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

using NUnit.Framework;
using XenAPI;
using System.Collections.Generic;
using XenAdmin;
using XenAdmin.Controls;
using XenAdmin.Model;

namespace XenAdminTests.TabsAndMenus
{
    [TestFixture, Category(TestCategories.UICategoryB)]
    public class TabsAndMenusCowley : TabsAndMenus
    {
        public TabsAndMenusCowley()
            : base("Cowley-db.xml")
        {
        }

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            DisableAllPlugins();
        }

        public static string[] XenCenterTabs = new string[] { "Home", "Search", "Tags", "Logs" };
        public static string[] PoolTabs = new string[] { "Search", "General", "Memory", "Storage", "Networking", "HA", "WLB", "Users", "Logs" };
        public static string[] HostTabs = new string[] { "Search", "General", "Memory", "Storage", "Networking", "NICs", "Console", "Performance", "Users", "Logs" };
        public static string[] VMTabs = new string[] { "General", "Memory", "Storage", "Networking", "Console", "Performance", "Snapshots", "Logs" };
        public static string[] DefaultTemplateTabs = new string[] { "General", "Memory", "Networking", "Logs" };
        public static string[] OtherInstallMediaTabs = new string[] { "General", "Memory", "Storage", "Networking", "Logs" };
        public static string[] UserTemplateTabs = new string[] { "General", "Memory", "Storage", "Networking", "Logs" };
        public static string[] SRTabs = new string[] { "General", "Storage", "Logs" };
        public static string[] SnapshotTabs = new string[] { "General", "Memory", "Networking", "Logs" };
        public static string[] VDITabs = new string[] { "Logs" };
        public static string[] NetworkTabs = new string[] { "Logs" };
        public static string[] GroupingTagTabs = new string[] { "Search", "Logs" };
        public static string[] FolderTabs = new string[] { "Search", "Logs" };

        [Test]
        public void Tabs_XenCenterNode()
        {
            VerifyTabs(null, XenCenterTabs);
        }

        [Test]
        public void Tabs_Pool()
        {
            VerifyTabs(GetAnyPool(), PoolTabs);
        }

        [Test]
        public void Tabs_Host()
        {
            foreach (Host host in GetAllXenObjects<Host>())
            {
                VerifyTabs(host, HostTabs);
            }
        }

        [Test]
        public void Tabs_VM()
        {
            foreach (VM vm in GetAllXenObjects<VM>(v => !v.is_a_template && !v.is_control_domain))
            {
                VerifyTabs(vm, VMTabs);
            }
        }

        [Test]
        public void Tabs_DefaultTemplate()
        {
            EnsureChecked(MainWindowWrapper.ViewMenuItems.TemplatesToolStripMenuItem);
            VerifyTabs(GetAnyDefaultTemplate(), DefaultTemplateTabs);
        }

        [Test]
        public void Tabs_OtherInstallMedia()
        {
            EnsureChecked(MainWindowWrapper.ViewMenuItems.TemplatesToolStripMenuItem);
            VerifyTabs(GetAnyDefaultTemplate(v => v.name_label == "Other install media"), OtherInstallMediaTabs);
        }


        [Test]
        public void Tabs_UserTemplate()
        {
            EnsureChecked(MainWindowWrapper.ViewMenuItems.TemplatesToolStripMenuItem);
            foreach (VM vm in GetAllXenObjects<VM>(v =>!v.IsHidden &&v.is_a_template && !v.DefaultTemplate && !v.is_a_snapshot))
            {
                VerifyTabs(vm, UserTemplateTabs);
            }
        }

        [Test]
        public void Tabs_SR()
        {
            EnsureChecked(MainWindowWrapper.ViewMenuItems.LocalStorageToolStripMenuItem);
            foreach (SR sr in GetAllXenObjects<SR>(s => !s.IsToolsSR))
            {
                VerifyTabs(sr, SRTabs);
            }
        }

        [Test]
        public void Tabs_Snapshot()
        {
            PutInOrgView(true);
            try
            {
                foreach (VM snapshot in GetAllXenObjects<VM>(v => v.is_a_snapshot))
                {
                    VerifyTabs(snapshot, SnapshotTabs);
                }
            }
            finally
            {
                PutInOrgView(false);
            }
        }

        [Test]
        public void Tabs_VDI()
        {
            PutInOrgView(true);
            try
            {
                VerifyTabs(GetAnyVDI(v => v.name_label != "base copy"), VDITabs);
            }
            finally
            {
                PutInOrgView(false);
            }
        }

        [Test]
        public void Tabs_Network()
        {
            PutInOrgView(true);
            try
            {
                foreach (XenAPI.Network network in GetAllXenObjects<XenAPI.Network>(n => n.name_label != "Guest installer network"))
                {
                    VerifyTabs(network, NetworkTabs);
                }
            }
            finally
            {
                PutInOrgView(false);
            }
        }

        [Test]
        public void Tabs_GroupingTag()
        {
            PutInOrgView(true);
            try
            {
                VirtualTreeNode n = GetAllTreeNodes().Find(v => v.Tag is GroupingTag);
                VerifyTabs((GroupingTag)n.Tag, GroupingTagTabs);
            }
            finally
            {
                PutInOrgView(false);
            }
        }

        public void Tabs_Folder()
        {
            PutInOrgView(true);
            try
            {
                foreach (Folder folder in GetAllXenObjects<Folder>())
                {
                    VerifyTabs(folder, FolderTabs);
                }
            }
            finally
            {
                PutInOrgView(false);
            }
        }

        [Test]
        public void ContextMenu_XenCenterNode_AllClosed()
        {
            new TabsAndMenusGeorge().ContextMenu_XenCenterNode_AllClosed();
        }

        [Test]
        public void ContextMenu_XenCenterNode_RootOpen()
        {
            new TabsAndMenusGeorge().ContextMenu_XenCenterNode_RootOpen();
        }

        [Test]
        public void ContextMenu_XenCenterNode_PoolOpen()
        {
            new TabsAndMenusGeorge().ContextMenu_XenCenterNode_PoolOpen();
        }

        [Test]
        public void ContextMenu_XenCenterNode_AllOpen()
        {
            new TabsAndMenusGeorge().ContextMenu_XenCenterNode_AllOpen();
        }

        [Test]
        public void ContextMenu_Pool()
        {
            new TabsAndMenusGeorge().ContextMenu_Pool();
        }

        [Test]
        public void ContextMenu_Master()
        {
            new TabsAndMenusGeorge().ContextMenu_Master();
        }

       

        [Test]
        public void ContextMenu_VMWithTools()
        {
            new TabsAndMenusGeorge().ContextMenu_VMWithTools();
        }





        [Test]
        public void ContextMenu_SR()
        {
            new TabsAndMenusGeorge().ContextMenu_SR();
        }

        [Test]
        public void ContextMenu_DefaultTemplate()
        {
            new TabsAndMenusGeorge().ContextMenu_DefaultTemplate();
        }

        [Test]
        public void ContextMenu_UserTemplate()
        {
            EnsureChecked(MainWindowWrapper.ViewMenuItems.TemplatesToolStripMenuItem);

            foreach (VM vm in GetAllXenObjects<VM>(v => v.InstantTemplate))
            {
                VerifyContextMenu(vm, new ExpectedMenuItem[] {
                    new ExpectedTextMenuItem("&New VM wizard...", true),
                    new ExpectedSeparator(),
                    new ExpectedTextMenuItem("&Export to File...", true),
                    new ExpectedTextMenuItem("&Copy", true),
                    new ExpectedSeparator(),
                    new ExpectedTextMenuItem("&Delete Template...", true),
                    new ExpectedSeparator(),
                    new ExpectedTextMenuItem("Properties", true)
                });
            }
        }

        [Test]
        public void ContextMenu_Snapshot()
        {
            new TabsAndMenusGeorge().ContextMenu_Snapshot();
        }

        //[Test]
        //public void ContextMenu_VDI()
        //{
        //    new TabsAndMenusGeorge().ContextMenu_VDI();
        //}

        //[Test]
        //public void ContextMenu_Network()
        //{
        //    new TabsAndMenusGeorge().ContextMenu_Network();
        //}

        //[Test]
        //public void ContextMenu_GroupingTag()
        //{
        //    new TabsAndMenusGeorge().ContextMenu_GroupingTag();
        //}

        //[Test]
        //public void ContextMenu_Folder()
        //{
        //    new TabsAndMenusGeorge().ContextMenu_Folder();
        //}

        [Test]
        public void TestPowerStateChangeUpdatesToolBar()
        {
            // select a running VM
            VM vm = GetAnyVM(v => v.power_state == vm_power_state.Running);

            Assert.IsTrue(SelectInTree(vm), "Couldn't select VM");

            // assert that start button is disabled (as VM is running.)
            Assert.IsTrue(!MainWindowWrapper.MainToolStripItems.StartVMToolStripButton.Enabled, "Start button should be disabled.");

            // click the force-shutdown menu item from the VM menu.
            MW(delegate
            {
                MainWindowWrapper.VMMenu.ShowDropDown();
                MainWindowWrapper.VMMenuItems.StartShutdownMenu.ShowDropDown();
                MainWindowWrapper.VMMenuItems.StartShutdownMenuItems.ForceShutdownToolStripMenuItem.PerformClick();
            });

            // assert it has halted and that the start-vm toolbar button has become enabled.
            MWWaitFor(() => vm.power_state == vm_power_state.Halted && MainWindowWrapper.MainToolStripItems.StartVMToolStripButton.Enabled, "Toolbar wasn't updated on VM shutdown.");

            // now restart VM.
            MW(MainWindowWrapper.MainToolStripItems.StartVMToolStripButton.PerformClick);
            MWWaitFor(() => vm.power_state == vm_power_state.Running, "Couldn't start VM.");
        }
    }
}
