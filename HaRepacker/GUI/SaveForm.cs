﻿/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.IO;
using MapleLib.WzLib.Util;
using System.Diagnostics;
using HaRepacker.GUI.Panels;
using MapleLib.MapleCryptoLib;
using System.Linq;

namespace HaRepacker.GUI
{
    public partial class SaveForm : Form
    {
        private readonly WzNode wzNode;

        private readonly WzFile wzf; // it can either be a WzImage or a WzFile only.
        private readonly WzImage wzImg; // it can either be a WzImage or a WzFile only.

        private readonly bool IsRegularWzFile = false; // or data.wz

        public string path;
        private readonly MainPanel panel;


        private bool bIsLoading = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="panel"></param>
        /// <param name="wzNode"></param>
        public SaveForm(MainPanel panel, WzNode wzNode)
        {
            InitializeComponent();

            MainForm.AddWzEncryptionTypesToComboBox(encryptionBox);

            this.wzNode = wzNode;
            if (wzNode.Tag is WzImage)
            {
                this.wzImg = (WzImage)wzNode.Tag;
                this.IsRegularWzFile = false;

                versionBox.Enabled = false;

            }
            else
            {
                this.wzf = (WzFile)wzNode.Tag;
                this.IsRegularWzFile = true;
            }
            this.panel = panel;
        }

        /// <summary>
        /// On loading
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveForm_Load(object sender, EventArgs e)
        {
            bIsLoading = true;

            try
            {
                if (this.IsRegularWzFile)
                {
                    encryptionBox.SelectedIndex = MainForm.GetIndexByWzMapleVersion(wzf.MapleVersion);
                    versionBox.Value = wzf.Version;
                }
                else
                { // Data.wz uses BMS encryption... no sepcific version indicated
                    encryptionBox.SelectedIndex = MainForm.GetIndexByWzMapleVersion(WzMapleVersion.BMS);
                }
            } finally
            {
                bIsLoading = false;
            }
        }

        /// <summary>
        /// Process command key on the form
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // ...
            if (keyData == (Keys.Escape))
            {
                Close(); // exit window
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }


        /// <summary>
        /// On encryption box selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void encryptionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bIsLoading)
                return;

            int selectedIndex = encryptionBox.SelectedIndex;
            WzMapleVersion wzMapleVersion = MainForm.GetWzMapleVersionByWzEncryptionBoxSelection(selectedIndex);
            if (wzMapleVersion == WzMapleVersion.CUSTOM)
            {
                CustomWZEncryptionInputBox customWzInputBox = new CustomWZEncryptionInputBox();
                customWzInputBox.ShowDialog();
            } else
            {
                MapleCryptoConstants.UserKey_WzLib = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.ToArray();
            }
        }

        /// <summary>
        /// On save button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (versionBox.Value < 0)
            {
                Warning.Error(HaRepacker.Properties.Resources.SaveVersionError);
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectOutWz,
                FileName = wzNode.Text,
                Filter = string.Format("{0}|*.wz",
                HaRepacker.Properties.Resources.WzFilter)
            })
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                WzMapleVersion wzMapleVersionSelected = MainForm.GetWzMapleVersionByWzEncryptionBoxSelection(encryptionBox.SelectedIndex); // new encryption selected
                if (this.IsRegularWzFile)
                {
                    if (wzf is WzFile file && wzf.MapleVersion != wzMapleVersionSelected)
                        PrepareAllImgs(file.WzDirectory);

                    wzf.MapleVersion = wzMapleVersionSelected;
                    if (wzf is WzFile file1)
                    {
                        file1.Version = (short)versionBox.Value;
                    }

                    if (wzf.FilePath != null && wzf.FilePath.ToLower() == dialog.FileName.ToLower())
                    {
                        wzf.SaveToDisk(dialog.FileName + "$tmp", wzMapleVersionSelected);
                        wzNode.DeleteWzNode();
                        try
                        {
                            File.Delete(dialog.FileName);
                            File.Move(dialog.FileName + "$tmp", dialog.FileName);
                        }catch(IOException ex)
                        {
                            MessageBox.Show("Handle error overwriting WZ file", HaRepacker.Properties.Resources.Error);
                        }
                    }
                    else
                    {
                        wzf.SaveToDisk(dialog.FileName, wzMapleVersionSelected);
                        wzNode.DeleteWzNode();
                    }

                    // Reload the new file
                    WzFile loadedWzFile = Program.WzFileManager.LoadWzFile(dialog.FileName, wzMapleVersionSelected);
                    if (loadedWzFile != null)
                        Program.WzFileManager.AddLoadedWzFileToMainPanel(loadedWzFile, panel);
                }
                else
                {
                    byte[] WzIv = WzTool.GetIvByMapleVersion(wzMapleVersionSelected);

                    // Save file
                    string tmpFilePath = dialog.FileName + ".tmp";
                    string targetFilePath = dialog.FileName;

                    bool error_noAdminPriviledge = false;
                    try
                    {
                        using (FileStream oldfs = File.Open(tmpFilePath, FileMode.OpenOrCreate))
                        {
                            using (WzBinaryWriter wzWriter = new WzBinaryWriter(oldfs, WzIv))
                            {
                                wzImg.SaveImage(wzWriter, true); // Write to temp folder
                            }
                        }
                        try
                        {
                            File.Copy(tmpFilePath, targetFilePath, true);
                            File.Delete(tmpFilePath);
                        }
                        catch (Exception exp)
                        {
                            Debug.WriteLine(exp); // nvm, dont show to user
                        }
                        wzNode.DeleteWzNode();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        error_noAdminPriviledge = true;
                    }

                    // Reload the new file
                    WzImage img = Program.WzFileManager.LoadDataWzHotfixFile(dialog.FileName, wzMapleVersionSelected, panel);
                    if (img == null || error_noAdminPriviledge)
                    {
                        MessageBox.Show(HaRepacker.Properties.Resources.MainFileOpenFail, HaRepacker.Properties.Resources.Error);
                    }
                }
            }
            Close();
        }


        private void PrepareAllImgs(WzDirectory dir)
        {
            foreach (WzImage img in dir.WzImages)
            {
                img.Changed = true;
            }
            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                PrepareAllImgs(subdir);
            }
        }
    }
}