﻿/* Copyright (c) Citrix Systems Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using XenAdmin.Actions;
using XenAdmin.Core;
using XenAdmin.Network;
using XenAdmin.Wizards.GenericPages;
using XenAPI;
using XenAdmin.Dialogs;
using XenAdmin.Wizards.NewSRWizard_Pages;
using XenAdmin.Wizards.NewSRWizard_Pages.Frontends;
using XenAdmin.Controls;
using System.Drawing;
using XenAdmin.Actions.DR;

namespace XenAdmin.Wizards
{
    public partial class NewSRWizard : XenWizardBase
    {
        #region Wizard pages
        private readonly NewSrWizardNamePage xenTabPageSrName;
        private readonly CIFS_ISO xenTabPageCifsIso;
        private readonly CSLG xenTabPageCslg;
        private readonly VHDoNFS xenTabPageVhdoNFS;
        private readonly NFS_ISO xenTabPageNfsIso;
        private readonly NetApp xenTabPageNetApp;
        private readonly EqualLogic xentabPageEqualLogic;
        private readonly LVMoISCSI xenTabPageLvmoIscsi;
        private readonly LVMoHBA xenTabPageLvmoHba;
        private readonly CslgSettings xenTabPageCslgSettings;
        private readonly CslgLocation xenTabPageCslgLocation;
        private readonly FilerDetails xenTabPageFilerDetails;
        private readonly ChooseSrTypePage xenTabPageChooseSrType;
        private readonly RBACWarningPage xenTabPageRbacWarning;
        #endregion

        /// <summary>
        /// The final action for this wizard is handled in this class, but the front end pages sometimes need to know when it's done so they
        /// allow the user to leave them.
        /// </summary>
        public AsyncAction FinalAction;

        private readonly string m_text;

        // For SR Reconfiguration
        private readonly SR _srToReattach;
        private SrWizardType m_srWizardType;

        private readonly bool _rbac;

        public NewSRWizard(IXenConnection connection)
            : this(connection, null, null)
        {
        }

        public NewSRWizard(IXenConnection connection, SR srToReattach)
            : this(connection, srToReattach, null)
        {
        }

        public NewSRWizard(IXenConnection connection, SR srToReattach, IStorageLinkObject storageLinkObject)
            : this(connection, srToReattach, storageLinkObject, false)
        {
        }

        internal NewSRWizard(IXenConnection connection, SR srToReattach, IStorageLinkObject storageLinkObject, bool disasterRecoveryTask)
            : base(connection)
        {
            InitializeComponent();

            xenTabPageSrName = new NewSrWizardNamePage();
            xenTabPageCifsIso = new CIFS_ISO();
            xenTabPageCslg = new CSLG();
            xenTabPageVhdoNFS = new VHDoNFS();
            xenTabPageNfsIso = new NFS_ISO();
            xenTabPageNetApp = new NetApp();
            xentabPageEqualLogic = new EqualLogic();
            xenTabPageLvmoIscsi = new LVMoISCSI();
            xenTabPageLvmoHba = new LVMoHBA();
            xenTabPageCslgSettings = new CslgSettings();
            xenTabPageCslgLocation = new CslgLocation();
            xenTabPageFilerDetails = new FilerDetails();
            xenTabPageChooseSrType = new ChooseSrTypePage();
            xenTabPageRbacWarning = new RBACWarningPage((srToReattach == null && !disasterRecoveryTask)
                             ? Messages.RBAC_WARNING_PAGE_DESCRIPTION_SR_CREATE
                             : Messages.RBAC_WARNING_PAGE_DESCRIPTION_SR_ATTACH);

            if (connection == null)
                Util.ThrowIfParameterNull(storageLinkObject, "storageLinkObject");
            if (storageLinkObject == null)
                Util.ThrowIfParameterNull(connection, "connection");
            if (storageLinkObject != null && connection != null)
                throw new ArgumentException("connection must be null when passing in a storageLinkObject", "connection");

            //do not use virtual members in constructor
            var format = (srToReattach == null && !disasterRecoveryTask)
                             ? Messages.NEWSR_TEXT
                             : Messages.NEWSR_TEXT_ATTACH;
            m_text = string.Format(format, xenConnection == null ? storageLinkObject.ToString() : Helpers.GetName(xenConnection));

            _srToReattach = srToReattach;
            
            xenTabPageChooseSrType.SrToReattach = srToReattach;
            xenTabPageChooseSrType.DisasterRecoveryTask = disasterRecoveryTask;
            xenTabPageCslg.SetStorageLinkObject(storageLinkObject);

            // Order the tab pages
            AddPage(xenTabPageChooseSrType);
            AddPage(xenTabPageSrName);
            AddPage(new XenTabPage {Text = Messages.NEWSR_LOCATION});

            // RBAC warning page 
            _rbac = (xenConnection != null && !xenConnection.Session.IsLocalSuperuser) &&
                   Helpers.GetMaster(xenConnection).external_auth_type != Auth.AUTH_TYPE_NONE;            
            if (_rbac)
            {
                // if reattaching, add "Permission checks" page after "Name" page, otherwise as first page (Ref. CA-61525)
                if (_srToReattach != null)
                    AddAfterPage(xenTabPageSrName, xenTabPageRbacWarning);
                else
                    AddPage(xenTabPageRbacWarning, 0);
                ConfigureRbacPage(disasterRecoveryTask);
            }
        }

        private void ConfigureRbacPage(bool disasterRecoveryTask)
        {
            if (!_rbac)
                return;

            xenTabPageRbacWarning.Connection = xenConnection;

            xenTabPageRbacWarning.ClearPermissionChecks();

            var warningMessage = (_srToReattach == null && !disasterRecoveryTask)
                             ? Messages.RBAC_WARNING_SR_WIZARD_CREATE
                             : Messages.RBAC_WARNING_SR_WIZARD_ATTACH;

            RBACWarningPage.WizardPermissionCheck check =
                new RBACWarningPage.WizardPermissionCheck(warningMessage) { Blocking = true };

            

            check.AddApiCheckRange(new RbacMethodList("SR.probe"));

            if (_srToReattach == null)
            {
                // create
                check.AddApiCheckRange(SrCreateAction.StaticRBACDependencies);
            }
            else if (disasterRecoveryTask && SR.SupportsDatabaseReplication(xenConnection, _srToReattach))
            {
                // "Attach SR needed for DR" case
                check.AddApiCheckRange(DrTaskCreateAction.StaticRBACDependencies);
            } 
            else 
            {
                // reattach
                check.AddApiCheckRange(SrReattachAction.StaticRBACDependencies);
            }

            xenTabPageRbacWarning.AddPermissionChecks(xenConnection, check);
        }

        private bool SetFCDevicesOnLVMoHBAPage()
        {
            List<FibreChannelDevice> devices;
            var success = LVMoHBA.FiberChannelScan(this, xenConnection, out devices);
            xenTabPageLvmoHba.FCDevices = devices;
            return success;
        }

        protected override bool RunNextPagePrecheck(XenTabPage senderPage)
        {
            // if reattaching and RBAC warning page is visible, then we run the prechecks when leaving the RBAC warning page
            // otherwise, when leaving xenTabPageSrName (Ref. CA-61525)
            bool runPrechecks = _srToReattach != null && _rbac
                                    ? senderPage == xenTabPageRbacWarning
                                    : senderPage == xenTabPageSrName;

            if (runPrechecks)
            {
                if (m_srWizardType is SrWizardType_LvmoHba)
                {
                    return SetFCDevicesOnLVMoHBAPage();
                }
                if (m_srWizardType is SrWizardType_Cslg || m_srWizardType is SrWizardType_NetApp || m_srWizardType is SrWizardType_EqualLogic)
                {
                    xenTabPageCslg.SrWizardType = m_srWizardType;
                    return xenTabPageCslg.PerformStorageSystemScan();
                }
            }
            
            return base.RunNextPagePrecheck(senderPage);
        }
     
        protected override void UpdateWizardContent(XenTabPage senderPage)
        {
            var senderPagetype = senderPage.GetType();

            if (senderPagetype == typeof(ChooseSrTypePage))
            {
                #region
                RemovePagesFrom(_rbac ? 3 : 2);
                m_srWizardType = xenTabPageChooseSrType.SrWizardType;

                if (m_srWizardType is SrWizardType_VhdoNfs)
                    AddPage(xenTabPageVhdoNFS);
                else if (m_srWizardType is SrWizardType_LvmoIscsi)
                    AddPage(xenTabPageLvmoIscsi);
                else if (m_srWizardType is SrWizardType_LvmoHba)
                    AddPage(xenTabPageLvmoHba);
                else if (m_srWizardType is SrWizardType_Cslg)
                {
                    AddPage(xenTabPageCslg);

                    if (Helpers.BostonOrGreater(xenConnection))
                        AddPages(xenTabPageCslgLocation, xenTabPageCslgSettings);
                    else
                        AddPages(new XenTabPage {Text = ""});
                }
                else if (m_srWizardType is SrWizardType_NetApp || m_srWizardType is SrWizardType_EqualLogic)
                {
                        AddPages(xenTabPageCslg, xenTabPageFilerDetails);

                        if (m_srWizardType is SrWizardType_NetApp)
                    {
                        xenTabPageFilerDetails.IsNetApp = true;
                            AddPage(xenTabPageNetApp);
                    }
                        else if (m_srWizardType is SrWizardType_EqualLogic)
                    {
                        xenTabPageFilerDetails.IsNetApp = false;
                            AddPage(xentabPageEqualLogic);
                    }
                }
                else if (m_srWizardType is SrWizardType_CifsIso)
                    AddPage(xenTabPageCifsIso);
                else if (m_srWizardType is SrWizardType_NfsIso)
                    AddPage(xenTabPageNfsIso);

                xenTabPageSrName.SrWizardType = m_srWizardType;
                xenTabPageSrName.MatchingFrontends = xenTabPageChooseSrType.MatchingFrontends;

                NotifyNextPagesOfChange(xenTabPageSrName);
                #endregion
            }
            else if (senderPagetype == typeof(NewSrWizardNamePage))
            {
                #region
                m_srWizardType.SrName = xenTabPageSrName.SrName;
                m_srWizardType.Description = xenTabPageSrName.SrDescription;
                m_srWizardType.AutoDescriptionRequired = xenTabPageSrName.AutoDescriptionRequired;

                if (m_srWizardType is SrWizardType_VhdoNfs)
                    xenTabPageVhdoNFS.SrWizardType = m_srWizardType;
                else if (m_srWizardType is SrWizardType_LvmoIscsi)
                    xenTabPageLvmoIscsi.SrWizardType = m_srWizardType;
                else if (m_srWizardType is SrWizardType_LvmoHba)
                    xenTabPageLvmoHba.SrWizardType = m_srWizardType;
                else if (m_srWizardType is SrWizardType_Cslg || m_srWizardType is SrWizardType_NetApp || m_srWizardType is SrWizardType_EqualLogic)
                    xenTabPageCslg.SrWizardType = m_srWizardType;
                else if (m_srWizardType is SrWizardType_CifsIso)
                    xenTabPageCifsIso.SrWizardType = m_srWizardType;
                else if (m_srWizardType is SrWizardType_NfsIso)
                    xenTabPageNfsIso.SrWizardType = m_srWizardType;
                #endregion
            }
            else if (senderPagetype == typeof(CIFS_ISO))
            {
                m_srWizardType.DeviceConfig = xenTabPageCifsIso.DeviceConfig;
                SetCustomDescription(m_srWizardType, xenTabPageCifsIso.SrDescription);
            }
            else if (senderPagetype == typeof(LVMoHBA))
            {
                string description = m_srWizardType.Description;
                string name = m_srWizardType.SrName;

                List<string> names = xenConnection.Cache.SRs.Select(sr => sr.Name).ToList();

                m_srWizardType.SrDescriptors.Clear();
                foreach (var lvmOhbaSrDescriptor in xenTabPageLvmoHba.SrDescriptors)
                {
                    m_srWizardType.SrDescriptors.Add(new SrDescriptor()
                                                         {
                                                             UUID = lvmOhbaSrDescriptor.UUID,
                                                             DeviceConfig = lvmOhbaSrDescriptor.DeviceConfig,
                                                             Description =
                                                                 description ?? lvmOhbaSrDescriptor.Description,
                                                             Name = name
                                                         });
                    names.Add(name);
                    name = SrWizardHelpers.DefaultSRName(Messages.NEWSR_HBA_DEFAULT_NAME, names);
                }
            }
            else if (senderPagetype == typeof(LVMoISCSI))
            {
                m_srWizardType.UUID = xenTabPageLvmoIscsi.UUID;
                m_srWizardType.DeviceConfig = xenTabPageLvmoIscsi.DeviceConfig;
                SetCustomDescription(m_srWizardType, xenTabPageLvmoIscsi.SrDescription);
            }
            else if (senderPagetype == typeof(NFS_ISO))
            {
                m_srWizardType.DeviceConfig = xenTabPageNfsIso.DeviceConfig;
                SetCustomDescription(m_srWizardType, xenTabPageNfsIso.SrDescription);
            }
            else if (senderPagetype == typeof(VHDoNFS))
            {
                m_srWizardType.UUID = xenTabPageVhdoNFS.UUID;
                m_srWizardType.DeviceConfig = xenTabPageVhdoNFS.DeviceConfig;
                SetCustomDescription(m_srWizardType, xenTabPageVhdoNFS.SrDescription);
            }
            else if (senderPagetype == typeof(CSLG))
            {
                #region
                if (Helpers.BostonOrGreater(xenConnection))
                {
                    xenTabPageCslgLocation.SelectedStorageAdapter = xenTabPageCslg.SelectedStorageAdapter;
                    xenTabPageCslgSettings.SelectedStorageAdapter = xenTabPageCslg.SelectedStorageAdapter;
                    NotifyNextPagesOfChange(xenTabPageCslgLocation, xenTabPageCslgSettings);
                }
                else
                {
                    RemovePagesFrom(_rbac ? 4 : 3);

                    if (xenTabPageCslg.SelectedStorageSystem != null)
                    {
                        AddPages(xenTabPageCslgSettings);
                        xenTabPageCslgSettings.SystemStorage = xenTabPageCslg.SelectedStorageSystem;
                        xenTabPageCslgSettings.StoragePools = xenTabPageCslg.StoragePools;
                        NotifyNextPagesOfChange(xenTabPageCslgLocation);
                    }
                    else if (xenTabPageCslg.NetAppSelected || xenTabPageCslg.DellSelected)
                    {
                        AddPage(xenTabPageFilerDetails);
                        NotifyNextPagesOfChange(xenTabPageFilerDetails);

                        if (xenTabPageCslg.NetAppSelected)
                        {
                            if (m_srWizardType is SrWizardType_Cslg)
                            {
                                m_srWizardType = ((SrWizardType_Cslg)m_srWizardType).ToNetApp();
                                xenTabPageCslg.SrWizardType = m_srWizardType;
                            }
                            xenTabPageFilerDetails.IsNetApp = true;
                            AddPage(xenTabPageNetApp);
                        }
                        else if (xenTabPageCslg.DellSelected)
                        {
                            if (m_srWizardType is SrWizardType_Cslg)
                            {
                                m_srWizardType = ((SrWizardType_Cslg)m_srWizardType).ToEqualLogic();
                                xenTabPageCslg.SrWizardType = m_srWizardType;
                            }
                            xenTabPageFilerDetails.IsNetApp = false;
                            AddPage(xentabPageEqualLogic);
                        }
                    }
                }

                foreach (var entry in xenTabPageCslg.DeviceConfigParts)
                    m_srWizardType.DeviceConfig[entry.Key] = entry.Value;
                #endregion
            }
            else if (senderPagetype == typeof(CslgLocation))
            {
                xenTabPageCslgSettings.StorageLinkCredentials = xenTabPageCslgLocation.StorageLinkCredentials;
                xenTabPageCslgSettings.SystemStorage = xenTabPageCslgLocation.SystemStorage;
                xenTabPageCslgSettings.StoragePools = xenTabPageCslgLocation.StoragePools;

                foreach (var entry in xenTabPageCslgLocation.DeviceConfigParts)
                    m_srWizardType.DeviceConfig[entry.Key] = entry.Value;
                NotifyNextPagesOfChange(xenTabPageCslgSettings);
            }
            else if (senderPagetype == typeof(CslgSettings))
            {
                foreach (var entry in xenTabPageCslgSettings.DeviceConfigParts)
                    m_srWizardType.DeviceConfig[entry.Key] = entry.Value;
                SetCustomDescription(m_srWizardType, xenTabPageCslgSettings.SrDescription);
            }
            else if (senderPagetype == typeof(FilerDetails))
            {
                #region
                foreach (var entry in xenTabPageFilerDetails.DeviceConfigParts)
                    m_srWizardType.DeviceConfig[entry.Key] = entry.Value;

                if (xenTabPageFilerDetails.IsNetApp)
                {
                    xenTabPageNetApp.SrScanAction = xenTabPageFilerDetails.SrScanAction;
                    xenTabPageNetApp.SrWizardType = m_srWizardType;
                    NotifyNextPagesOfChange(xenTabPageNetApp);
                }
                else
                {
                    xentabPageEqualLogic.SrScanAction = xenTabPageFilerDetails.SrScanAction;
                    xentabPageEqualLogic.SrWizardType = m_srWizardType;
                    NotifyNextPagesOfChange(xentabPageEqualLogic);
                }
                #endregion
            }
            else if (senderPagetype == typeof(NetApp))
            {
                m_srWizardType.UUID = xenTabPageNetApp.UUID;
                foreach (var entry in xenTabPageNetApp.DeviceConfigParts)
                    m_srWizardType.DeviceConfig[entry.Key] = entry.Value;
                SetCustomDescription(m_srWizardType, xenTabPageNetApp.SrDescription);
            }
            else if (senderPagetype == typeof(EqualLogic))
            {
                m_srWizardType.UUID = xentabPageEqualLogic.UUID;
                foreach (var entry in xentabPageEqualLogic.DeviceConfigParts)
                    m_srWizardType.DeviceConfig[entry.Key] = entry.Value;
                SetCustomDescription(m_srWizardType, xentabPageEqualLogic.SrDescription);
            }
        }

        private static void SetCustomDescription(SrWizardType srwizardtype, string description)
        {
            if (srwizardtype.Description == null)
                srwizardtype.Description = description;
        }

        protected override void FinishWizard()
        {
            FinalAction = null;

            // Override the WizardBase: try running the SR create/attach. If it succeeds, close the wizard.
            // Otherwise show the error and allow the user to adjust the settings and try again.
            Pool pool = Helpers.GetPoolOfOne(xenConnection);
            if (pool == null)
            {
                log.Error("New SR Wizard: Pool has disappeared");
                new ThreeButtonDialog(
                   new ThreeButtonDialog.Details(SystemIcons.Warning, string.Format(Messages.NEW_SR_CONNECTION_LOST, Helpers.GetName(xenConnection)), Messages.XENCENTER)).ShowDialog(this);
                
                base.FinishWizard();
                return;
            }

            Host master = xenConnection.Resolve(pool.master);
            if (master == null)
            {
                log.Error("New SR Wizard: Master has disappeared");
                new ThreeButtonDialog(
                   new ThreeButtonDialog.Details(SystemIcons.Warning, string.Format(Messages.NEW_SR_CONNECTION_LOST, Helpers.GetName(xenConnection)), Messages.XENCENTER)).ShowDialog(this);
                base.FinishWizard();
                return;
            }

            if (_srToReattach != null && !_srToReattach.IsDetached && _srToReattach.Connection == xenConnection)
            {
                // Error - cannot reattach attached SR
                MessageBox.Show(this,
                    String.Format(Messages.STORAGE_IN_USE, _srToReattach.Name, Helpers.GetName(xenConnection)),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);

                FinishCanceled();
                return;
            }

            // show warning prompt if required
            if (!AskUserIfShouldContinue())
            {
                FinishCanceled();
                return;
            }

            List<AsyncAction> actionList = GetActions(master, m_srWizardType.DisasterRecoveryTask);

            if (actionList.Count == 1)
                FinalAction = actionList[0];
            else
                FinalAction = new ParallelAction(xenConnection, Messages.NEW_SR_WIZARD_FINAL_ACTION_TITLE,
                                                 Messages.NEW_SR_WIZARD_FINAL_ACTION_START,
                                                 Messages.NEW_SR_WIZARD_FINAL_ACTION_END, actionList);

            // if this is a Disaster Recovery Task, it could be either a "Find existing SRs" or an "Attach SR needed for DR" case
            if (m_srWizardType.DisasterRecoveryTask)
            {
                base.FinishWizard();
                return;
            }

            ProgressBarStyle progressBarStyle = FinalAction is SrIntroduceAction ? ProgressBarStyle.Blocks : ProgressBarStyle.Marquee;
            ActionProgressDialog dialog = new ActionProgressDialog(FinalAction, progressBarStyle) {ShowCancel = true};
            dialog.ShowDialog(this);

            if (!FinalAction.Succeeded && FinalAction is SrReattachAction && !_srToReattach.IsDetached)
            {
                // reattach failed. Ensure SR is now detached.
                dialog = new ActionProgressDialog(new SrAction(SrActionKind.Detach, _srToReattach), progressBarStyle);
                dialog.ShowCancel = false;
                dialog.ShowDialog();
            }

            // If action failed and frontend wants to stay open, just return
            if (!FinalAction.Succeeded)
            {
                DialogResult = DialogResult.None;
                FinishCanceled();

                if (m_srWizardType.AutoDescriptionRequired)
                {
                    foreach (var srDescriptor in m_srWizardType.SrDescriptors)
                    {
                        srDescriptor.Description = null;
                    }
                }

                if (m_srWizardType is SrWizardType_LvmoHba)
                {
                    SetFCDevicesOnLVMoHBAPage();
                    CurrentStepTabPage.PopulatePage();
                }

                return;
            }

            // Close wizard
            base.FinishWizard();
        }

        private List<AsyncAction> GetActions(Host master, bool disasterRecoveryTask)
        {
            // Now we need to decide what to do.
            // This will be one off create, introduce, reattach

            List<AsyncAction> finalActions = new List<AsyncAction>();

            foreach (var srDescriptor in m_srWizardType.SrDescriptors)
            {
                if (String.IsNullOrEmpty(m_srWizardType.UUID))
                {
                    // Don't need to show any warning, as the only destructive creates
                    // are in iSCSI and HBA, where they show their own warning
                    finalActions.Add(new SrCreateAction(xenConnection, master,
                                                        srDescriptor.Name,
                                                        srDescriptor.Description,
                                                        m_srWizardType.Type,
                                                        m_srWizardType.ContentType,
                                                        !master.RestrictPoolAttachedStorage,
                                                        srDescriptor.DeviceConfig,
                                                        Program.StorageLinkConnections.GetCopy()));
                }
                else if (_srToReattach == null || _srToReattach.Connection != xenConnection)
                {
                    // introduce
                    if (disasterRecoveryTask &&
                        (_srToReattach == null || SR.SupportsDatabaseReplication(xenConnection, _srToReattach)))
                    {
                        // "Find existing SRs" or "Attach SR needed for DR" cases
                        ScannedDeviceInfo deviceInfo = new ScannedDeviceInfo(m_srWizardType.Type,
                                                                             srDescriptor.DeviceConfig,
                                                                             srDescriptor.UUID);
                        finalActions.Add(new DrTaskCreateAction(xenConnection, deviceInfo));
                    }
                    else
                        finalActions.Add(new SrIntroduceAction(xenConnection,
                                                               srDescriptor.UUID,
                                                               srDescriptor.Name,
                                                               srDescriptor.Description,
                                                               m_srWizardType.Type,
                                                               m_srWizardType.ContentType,
                                                               !master.RestrictPoolAttachedStorage,
                                                               srDescriptor.DeviceConfig));
                }
                else
                {
                    // Reattach
                    if (disasterRecoveryTask && SR.SupportsDatabaseReplication(xenConnection, _srToReattach))
                    {
                        // "Attach SR needed for DR" case
                        ScannedDeviceInfo deviceInfo = new ScannedDeviceInfo(_srToReattach.GetSRType(true),
                                                                             srDescriptor.DeviceConfig,
                                                                             _srToReattach.uuid);
                        finalActions.Add(new DrTaskCreateAction(xenConnection, deviceInfo));
                    }
                    else
                        finalActions.Add(new SrReattachAction(_srToReattach,
                                                              srDescriptor.Name,
                                                              srDescriptor.Description,
                                                              srDescriptor.DeviceConfig));
                }
            }

            return finalActions;
        }

        private bool AskUserIfShouldContinue()
        {
            if (!Program.RunInAutomatedTestMode && !String.IsNullOrEmpty(m_srWizardType.UUID))
            {
                if (_srToReattach == null)
                {
                    // introduce
                    if (m_srWizardType.ShowIntroducePrompt)
                    {
                        return DialogResult.Yes == new ThreeButtonDialog(
                                new ThreeButtonDialog.Details(SystemIcons.Warning, String.Format(Messages.NEWSR_MULTI_POOL_WARNING, m_srWizardType.UUID), Text),
                                ThreeButtonDialog.ButtonYes,
                                new ThreeButtonDialog.TBDButton(Messages.NO_BUTTON_CAPTION, DialogResult.No, ThreeButtonDialog.ButtonType.CANCEL, true)).ShowDialog(this);
                    }

                }
                else if (_srToReattach.Connection == xenConnection)
                {
                    // Reattach
                    if (m_srWizardType.ShowReattachWarning)
                    {
                        return DialogResult.Yes == new ThreeButtonDialog(
                            new ThreeButtonDialog.Details(SystemIcons.Warning, String.Format(Messages.NEWSR_MULTI_POOL_WARNING, _srToReattach.Name), Text),
                            ThreeButtonDialog.ButtonYes,
                            new ThreeButtonDialog.TBDButton(Messages.NO_BUTTON_CAPTION, DialogResult.No, ThreeButtonDialog.ButtonType.CANCEL, true)).ShowDialog(this);
                    }
                }
                else
                {
                    // uuid != null
                    // _srToReattach != null
                    // _srToReattach.Server.IsDetached
                    // _srToReattach.Connection != current connection

                    // Warn user SR is already attached to other pool, and then introduce to this pool 

                    return DialogResult.OK == new ThreeButtonDialog(
                        new ThreeButtonDialog.Details(
                            SystemIcons.Warning,
                            string.Format(Messages.ALREADY_ATTACHED_ELSEWHERE, _srToReattach.Name, Helpers.GetName(xenConnection), 
                            Text)),
                        ThreeButtonDialog.ButtonOK,
                        ThreeButtonDialog.ButtonCancel).ShowDialog(this);
                }
            }

            return true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            Text = m_text; //set here; do not set virtual members in constructor

            if (_srToReattach == null)
                return;

            if (xenTabPageChooseSrType.MatchingFrontends <= 0)
            {
                new ThreeButtonDialog(
                    new ThreeButtonDialog.Details(
                        SystemIcons.Error,
                        String.Format(Messages.CANNOT_FIND_SR_WIZARD_TYPE, _srToReattach.type),
                        Messages.XENCENTER)).ShowDialog(this);

                Close();
            }
            else if (xenTabPageChooseSrType.MatchingFrontends == 1)
            {
                // move to "Name" page
                NextStep();               
                // move to "Location" page or "Permission checks" page
                NextStep();
                // if rbac, stay on this page (Ref. CA-61525)
                if (_rbac)
                    return;
            }

            if (_srToReattach.type == "cslg" && Helpers.BostonOrGreater(_srToReattach.Connection)
                && xenTabPageCslg.SelectedStorageAdapter != null)
            {
                NextStep();
            }
        }

        protected override string WizardPaneHelpID()
        {
            return CurrentStepTabPage is RBACWarningPage ? FormatHelpId("Rbac") : base.WizardPaneHelpID();
        }

        public void CheckNFSISORadioButton()
        {
            xenTabPageChooseSrType.PreselectNewSrWizardType(typeof(SrWizardType_NfsIso));
        }
    }
}
