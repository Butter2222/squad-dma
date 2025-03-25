using SkiaSharp.Views.Desktop;

namespace squad_dma
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components is not null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            colDialog = new ColorDialog();
            toolTip = new ToolTip(components);
            btnToggleMap = new Button();
            chkShowMapSetup = new CheckBox();
            btnRestartRadar = new Button();
            btnDumpNames = new Button();
            chkShowAimview = new CheckBox();
            trkUIScale = new TrackBar();
            trkAimLength = new TrackBar();
            trkZoomSensivity = new TrackBar();
            tabSettings = new TabPage();
            grpConfig = new GroupBox();
            grpUserInterface = new GroupBox();
            lblZoomSensivity = new Label();
            lblAimline = new Label();
            lblUIScale = new Label();
            grpRadar = new GroupBox();
            tabRadar = new TabPage();            
             grpMapSetup = new GroupBox();
            btnApplyMapScale = new Button();
            chkMapFree = new CheckBox();
            txtMapSetupScale = new TextBox();
            lblMapScale = new Label();
            txtMapSetupY = new TextBox();
            lblMapXY = new Label();
            txtMapSetupX = new TextBox();
            lblMapCoords = new Label();
            tabControl = new TabControl();

            grpEsp = new GroupBox();
            lblTeam = new Label();
            teamComboBox = new ComboBox();

            ((System.ComponentModel.ISupportInitialize)trkUIScale).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkAimLength).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkZoomSensivity).BeginInit();
            tabSettings.SuspendLayout();
            grpConfig.SuspendLayout();
            grpUserInterface.SuspendLayout();
            grpRadar.SuspendLayout();
            tabRadar.SuspendLayout();
            grpMapSetup.SuspendLayout();
            tabControl.SuspendLayout();
            SuspendLayout();
            // 
            // colDialog
            // 
            colDialog.FullOpen = true;
            // 
            // btnToggleMap
            // 
            btnToggleMap.Location = new Point(236, 22);
            btnToggleMap.Margin = new Padding(4, 3, 4, 3);
            btnToggleMap.Name = "btnToggleMap";
            btnToggleMap.Size = new Size(107, 27);
            btnToggleMap.TabIndex = 7;
            btnToggleMap.Text = "Toggle Map (F5)";
            toolTip.SetToolTip(btnToggleMap, "Manually toggles active map");
            btnToggleMap.UseVisualStyleBackColor = true;
            btnToggleMap.Click += btnToggleMap_Click;
            // 
            // chkShowMapSetup
            // 
            chkShowMapSetup.AutoSize = true;
            chkShowMapSetup.Location = new Point(7, 22);
            chkShowMapSetup.Name = "chkShowMapSetup";
            chkShowMapSetup.Size = new Size(153, 19);
            chkShowMapSetup.TabIndex = 9;
            chkShowMapSetup.Text = "Show Map Setup Helper";
            toolTip.SetToolTip(chkShowMapSetup, "Shows the 'Map Setup' panel");
            chkShowMapSetup.UseVisualStyleBackColor = true;
            chkShowMapSetup.CheckedChanged += chkShowMapSetup_CheckedChanged;
            // 
            // btnRestartRadar
            // 
            btnRestartRadar.Font = new Font("Segoe UI", 9.75F);
            btnRestartRadar.Location = new Point(350, 22);
            btnRestartRadar.Name = "btnRestartRadar";
            btnRestartRadar.Size = new Size(107, 27);
            btnRestartRadar.TabIndex = 18;
            btnRestartRadar.Text = "Restart Radar";
            toolTip.SetToolTip(btnRestartRadar, "Manually triggers radar restart");
            btnRestartRadar.UseVisualStyleBackColor = true;
            btnRestartRadar.Click += btnRestartRadar_Click;
            // 
            // btnDumpNames
            // 
            btnDumpNames.AutoSize = true;
            btnDumpNames.Location = new Point(310, 22);
            btnDumpNames.Name = "btnDumpNames";
            btnDumpNames.Size = new Size(114, 25);
            btnDumpNames.TabIndex = 26;
            btnDumpNames.Text = "Dump Names (F6)";
            toolTip.SetToolTip(btnDumpNames, "Dumps entity names in the game instance");
            btnDumpNames.UseVisualStyleBackColor = true;
            btnDumpNames.Click += btnDumpNames_Click;
            // 
            // chkShowAimview
            // 
            chkShowAimview.AutoSize = true;
            chkShowAimview.Location = new Point(162, 26);
            chkShowAimview.Name = "chkShowAimview";
            chkShowAimview.Size = new Size(127, 19);
            chkShowAimview.TabIndex = 19;
            chkShowAimview.Text = "Show Aimview (F4)";
            toolTip.SetToolTip(chkShowAimview, "Displays the 3D aimview");
            chkShowAimview.UseVisualStyleBackColor = true;
            // 
            // trkUIScale
            // 
            trkUIScale.LargeChange = 10;
            trkUIScale.Location = new Point(25, 142);
            trkUIScale.Maximum = 200;
            trkUIScale.Minimum = 50;
            trkUIScale.Name = "trkUIScale";
            trkUIScale.Size = new Size(116, 45);
            trkUIScale.TabIndex = 27;
            trkUIScale.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkUIScale, "Scales the UI fonts etc, useful for larger screen resolutions");
            trkUIScale.Value = 100;
            trkUIScale.Scroll += trkUIScale_Scroll;
            // 
            // trkAimLength
            // 
            trkAimLength.LargeChange = 50;
            trkAimLength.Location = new Point(175, 142);
            trkAimLength.Margin = new Padding(4, 3, 4, 3);
            trkAimLength.Maximum = 2000;
            trkAimLength.Minimum = 10;
            trkAimLength.Name = "trkAimLength";
            trkAimLength.Size = new Size(114, 45);
            trkAimLength.SmallChange = 5;
            trkAimLength.TabIndex = 11;
            trkAimLength.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkAimLength, "Length of the 'bar' or 'aim line' on the localplayer");
            trkAimLength.Value = 500;
            trkAimLength.Scroll += trkAimLength_Scroll;
            // 
            // trkZoomSensivity
            // 
            trkZoomSensivity.LargeChange = 1;
            trkZoomSensivity.Location = new Point(320, 142);
            trkZoomSensivity.Maximum = 30;
            trkZoomSensivity.Minimum = 1;
            trkZoomSensivity.Name = "trkZoomSensivity";
            trkZoomSensivity.Size = new Size(118, 45);
            trkZoomSensivity.TabIndex = 29;
            trkZoomSensivity.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkZoomSensivity, "The map zoom sensivity");
            trkZoomSensivity.Value = 30;
            trkZoomSensivity.Scroll += trkZoomSensivity_Scroll;
            // 
            // tabSettings
            // 
            tabSettings.Controls.Add(grpConfig);
            tabSettings.Location = new Point(4, 24);
            tabSettings.Name = "tabSettings";
            tabSettings.Padding = new Padding(3);
            tabSettings.Size = new Size(1592, 872);
            tabSettings.TabIndex = 1;
            tabSettings.Text = "Settings";
            tabSettings.UseVisualStyleBackColor = true;
            // 
            // grpConfig
            // 
            grpConfig.Controls.Add(grpUserInterface);
            grpConfig.Controls.Add(grpRadar);
            grpConfig.Controls.Add(grpEsp); // ESP
            grpConfig.Dock = DockStyle.Fill;
            grpConfig.Location = new Point(3, 3);
            grpConfig.Margin = new Padding(4, 3, 4, 3);
            grpConfig.Name = "grpConfig";
            grpConfig.Padding = new Padding(4, 3, 4, 3);
            grpConfig.Size = new Size(1586, 866);
            grpConfig.TabIndex = 8;
            grpConfig.TabStop = false;
            grpConfig.Text = "Radar Config";
            // 
            // grpUserInterface
            // 
            grpUserInterface.Controls.Add(trkZoomSensivity);
            grpUserInterface.Controls.Add(lblZoomSensivity);
            grpUserInterface.Controls.Add(trkAimLength);
            grpUserInterface.Controls.Add(lblAimline);
            grpUserInterface.Controls.Add(lblUIScale);
            grpUserInterface.Controls.Add(trkUIScale);
            grpUserInterface.Controls.Add(chkShowAimview);
            grpUserInterface.Controls.Add(btnDumpNames);
            grpUserInterface.Location = new Point(5, 93);
            grpUserInterface.Name = "grpUserInterface";
            grpUserInterface.Size = new Size(463, 203);
            grpUserInterface.TabIndex = 26;
            grpUserInterface.TabStop = false;
            grpUserInterface.Text = "UI";

            //  ESP
            grpEsp.Location = new Point(5, 306); // grpUserInterface (93 + 203 + 10)
            grpEsp.Name = "grpEsp";
            grpEsp.Size = new Size(463, 80);
            grpEsp.TabIndex = 27;
            grpEsp.TabStop = false;
            grpEsp.Text = "ESP"; lblTeam.AutoSize = true;
            lblTeam.Location = new Point(10, 25);
            lblTeam.Name = "lblTeam";
            lblTeam.Size = new Size(60, 15);
            lblTeam.TabIndex = 0;
            lblTeam.Text = "Your Team:";

            teamComboBox.Location = new Point(80, 22);
            teamComboBox.Name = "teamComboBox";
            teamComboBox.Size = new Size(150, 23);
            teamComboBox.TabIndex = 1;
            teamComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            teamComboBox.Items.AddRange(Enum.GetNames(typeof(Team)));
            teamComboBox.SelectedIndex = Array.IndexOf(Enum.GetNames(typeof(Team)), "Unknown"); // Par défaut Unknown
            teamComboBox.SelectedIndexChanged += (s, e) => // Ajouter l'événement ici
            {
                //Program.Log($"ComboBox selection changed to Index: {teamComboBox.SelectedIndex}, Item: {teamComboBox.SelectedItem}");
                UpdateSelectedTeam(teamComboBox);
            };
            grpEsp.Controls.Add(lblTeam);
            grpEsp.Controls.Add(teamComboBox);

            // 
            // lblZoomSensivity
            // 
            lblZoomSensivity.AutoSize = true;
            lblZoomSensivity.Location = new Point(336, 124);
            lblZoomSensivity.Margin = new Padding(4, 0, 4, 0);
            lblZoomSensivity.Name = "lblZoomSensivity";
            lblZoomSensivity.Size = new Size(88, 15);
            lblZoomSensivity.TabIndex = 30;
            lblZoomSensivity.Text = "Zoom Sensivity";
            lblZoomSensivity.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblAimline
            // 
            lblAimline.AutoSize = true;
            lblAimline.Location = new Point(188, 124);
            lblAimline.Margin = new Padding(4, 0, 4, 0);
            lblAimline.Name = "lblAimline";
            lblAimline.Size = new Size(88, 15);
            lblAimline.TabIndex = 13;
            lblAimline.Text = "Aimline Length";
            lblAimline.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblUIScale
            // 
            lblUIScale.AutoSize = true;
            lblUIScale.Location = new Point(56, 124);
            lblUIScale.Name = "lblUIScale";
            lblUIScale.Size = new Size(48, 15);
            lblUIScale.TabIndex = 28;
            lblUIScale.Text = "UI Scale";
            lblUIScale.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // grpRadar
            // 
            grpRadar.Controls.Add(btnRestartRadar);
            grpRadar.Controls.Add(chkShowMapSetup);
            grpRadar.Controls.Add(btnToggleMap);
            grpRadar.Location = new Point(5, 22);
            grpRadar.Name = "grpRadar";
            grpRadar.Size = new Size(463, 65);
            grpRadar.TabIndex = 26;
            grpRadar.TabStop = false;
            grpRadar.Text = "Radar";
            // 
            // tabRadar
            // 
            tabRadar.Controls.Add(grpMapSetup);
            tabRadar.Location = new Point(4, 24);
            tabRadar.Name = "tabRadar";
            tabRadar.Padding = new Padding(3);
            tabRadar.Size = new Size(1592, 872);
            tabRadar.TabIndex = 0;
            tabRadar.Text = "Radar";
            tabRadar.UseVisualStyleBackColor = true;
            // 
            // grpMapSetup
            // 
            grpMapSetup.Controls.Add(btnApplyMapScale);
            grpMapSetup.Controls.Add(chkMapFree);
            grpMapSetup.Controls.Add(txtMapSetupScale);
            grpMapSetup.Controls.Add(lblMapScale);
            grpMapSetup.Controls.Add(txtMapSetupY);
            grpMapSetup.Controls.Add(lblMapXY);
            grpMapSetup.Controls.Add(txtMapSetupX);
            grpMapSetup.Controls.Add(lblMapCoords);
            grpMapSetup.Location = new Point(8, 6);
            grpMapSetup.Name = "grpMapSetup";
            grpMapSetup.Size = new Size(327, 175);
            grpMapSetup.TabIndex = 11;
            grpMapSetup.TabStop = false;
            grpMapSetup.Text = "Map Setup";
            grpMapSetup.Visible = false;
            // 
            // btnApplyMapScale
            // 
            btnApplyMapScale.Location = new Point(7, 130);
            btnApplyMapScale.Name = "btnApplyMapScale";
            btnApplyMapScale.Size = new Size(89, 30);
            btnApplyMapScale.TabIndex = 18;
            btnApplyMapScale.Text = "Apply";
            btnApplyMapScale.UseVisualStyleBackColor = true;
            btnApplyMapScale.Click += btnApplyMapScale_Click;
            // 
            // chkMapFree
            // 
            chkMapFree.Appearance = Appearance.Button;
            chkMapFree.AutoSize = true;
            chkMapFree.Location = new Point(0, 0);
            chkMapFree.Name = "chkMapFree";
            chkMapFree.Size = new Size(79, 25);
            chkMapFree.TabIndex = 17;
            chkMapFree.Text = "Map Follow";
            chkMapFree.TextAlign = ContentAlignment.MiddleCenter;
            chkMapFree.UseVisualStyleBackColor = true;
            chkMapFree.CheckedChanged += chkMapFree_CheckedChanged;
            // 
            // txtMapSetupScale
            // 
            txtMapSetupScale.Location = new Point(46, 101);
            txtMapSetupScale.Name = "txtMapSetupScale";
            txtMapSetupScale.Size = new Size(50, 23);
            txtMapSetupScale.TabIndex = 15;
            // 
            // lblMapScale
            // 
            lblMapScale.AutoSize = true;
            lblMapScale.Location = new Point(6, 104);
            lblMapScale.Name = "lblMapScale";
            lblMapScale.Size = new Size(34, 15);
            lblMapScale.TabIndex = 14;
            lblMapScale.Text = "Scale";
            // 
            // txtMapSetupY
            // 
            txtMapSetupY.Location = new Point(102, 67);
            txtMapSetupY.Name = "txtMapSetupY";
            txtMapSetupY.Size = new Size(50, 23);
            txtMapSetupY.TabIndex = 13;
            // 
            // lblMapXY
            // 
            lblMapXY.AutoSize = true;
            lblMapXY.Location = new Point(6, 70);
            lblMapXY.Name = "lblMapXY";
            lblMapXY.Size = new Size(24, 15);
            lblMapXY.TabIndex = 12;
            lblMapXY.Text = "X,Y";
            // 
            // txtMapSetupX
            // 
            txtMapSetupX.Location = new Point(46, 67);
            txtMapSetupX.Name = "txtMapSetupX";
            txtMapSetupX.Size = new Size(50, 23);
            txtMapSetupX.TabIndex = 11;
            // 
            // lblMapCoords
            // 
            lblMapCoords.AutoSize = true;
            lblMapCoords.Location = new Point(7, 19);
            lblMapCoords.Margin = new Padding(4, 0, 4, 0);
            lblMapCoords.Name = "lblMapCoords";
            lblMapCoords.Size = new Size(43, 15);
            lblMapCoords.TabIndex = 10;
            lblMapCoords.Text = "coords";
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabRadar);
            tabControl.Controls.Add(tabSettings);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1600, 900);
            tabControl.TabIndex = 8;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1600, 900);
            Controls.Add(tabControl);
            Margin = new Padding(4, 3, 4, 3);
            Name = "MainForm";
            Text = "I Love Squad";
            ((System.ComponentModel.ISupportInitialize)trkUIScale).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkAimLength).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkZoomSensivity).EndInit();
            tabSettings.ResumeLayout(false);
            grpConfig.ResumeLayout(false);
            grpUserInterface.ResumeLayout(false);
            grpUserInterface.PerformLayout();
            grpRadar.ResumeLayout(false);
            grpRadar.PerformLayout();
            tabRadar.ResumeLayout(false);
            grpMapSetup.ResumeLayout(false);
            grpMapSetup.PerformLayout();
            tabControl.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private ColorDialog colDialog;
        private ToolTip toolTip;
        private TabPage tabSettings;
        private GroupBox grpConfig;
        private GroupBox grpUserInterface;
        private TrackBar trkAimLength;
        private Label lblAimline;
        private Label lblUIScale;
        private TrackBar trkUIScale;
        private CheckBox chkShowAimview;
        private Button btnDumpNames;
        private GroupBox grpRadar;
        private Button btnRestartRadar;
        private CheckBox chkShowMapSetup;
        private Button btnToggleMap;
        private TabPage tabRadar;
        private GroupBox grpMapSetup;
        private Button btnApplyMapScale;
        private CheckBox chkMapFree;
        private TextBox txtMapSetupScale;
        private Label lblMapScale;
        private TextBox txtMapSetupY;
        private Label lblMapXY;
        private TextBox txtMapSetupX;
        private Label lblMapCoords;
        private TabControl tabControl;
        private TrackBar trkZoomSensivity;
        private Label lblZoomSensivity;

        private GroupBox grpEsp;         // Cadre ESP
        private Label lblTeam;           // Étiquette "Your Team"
        private ComboBox teamComboBox;
    }
}
