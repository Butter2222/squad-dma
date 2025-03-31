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
            chkShowEnemyDistance = new CheckBox();
            trkUIScale = new TrackBar();
            trkAimLength = new TrackBar();
            tabSettings = new TabPage();
            grpConfig = new GroupBox();
            grpUserInterface = new GroupBox();
            lblAimline = new Label();
            lblUIScale = new Label();
            grpRadar = new GroupBox();
            tabRadar = new TabPage();
            ticketsPanel = new Panel();
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

            //ESP 
            grpEsp = new GroupBox();
            chkEnableEsp = new CheckBox();
            trkEspMaxDistance = new TrackBar();
            lblEspMaxDistance = new Label();
            chkShowAllies = new CheckBox();
            chkEspShowNames = new CheckBox();
            chkEspShowDistance = new CheckBox();
            chkEspShowHealth = new CheckBox();
            txtEspFontSize = new TextBox();
            lblEspFontSize = new Label();
            txtEspColorA = new TextBox();
            txtEspColorR = new TextBox();
            txtEspColorG = new TextBox();
            txtEspColorB = new TextBox();
            lblEspColorA = new Label();
            lblEspColorR = new Label();
            lblEspColorG = new Label();
            lblEspColorB = new Label();

            ((System.ComponentModel.ISupportInitialize)trkUIScale).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkAimLength).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkEspMaxDistance).BeginInit();
            tabSettings.SuspendLayout();
            grpConfig.SuspendLayout();
            grpUserInterface.SuspendLayout();
            grpRadar.SuspendLayout();
            tabRadar.SuspendLayout();
            grpMapSetup.SuspendLayout();
            tabControl.SuspendLayout();
            grpEsp.SuspendLayout();
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
            // chkShowEnemyDistance
            // 
            chkShowEnemyDistance.AutoSize = true;
            chkShowEnemyDistance.Location = new Point(162, 26);
            chkShowEnemyDistance.Name = "chkShowEnemyDistance";
            chkShowEnemyDistance.Size = new Size(126, 19);
            chkShowEnemyDistance.TabIndex = 19;
            chkShowEnemyDistance.Text = "Show Distance (F4)";
            toolTip.SetToolTip(chkShowEnemyDistance, "Displays the distance for the enemy players");
            chkShowEnemyDistance.UseVisualStyleBackColor = true;
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
            grpUserInterface.Controls.Add(trkAimLength);
            grpUserInterface.Controls.Add(lblAimline);
            grpUserInterface.Controls.Add(lblUIScale);
            grpUserInterface.Controls.Add(trkUIScale);
            grpUserInterface.Controls.Add(chkShowEnemyDistance);
            grpUserInterface.Controls.Add(btnDumpNames);
            grpUserInterface.Location = new Point(5, 93);
            grpUserInterface.Name = "grpUserInterface";
            grpUserInterface.Size = new Size(463, 203);
            grpUserInterface.TabIndex = 26;
            grpUserInterface.TabStop = false;
            grpUserInterface.Text = "UI";

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
            tabRadar.Controls.Add(ticketsPanel);
            tabRadar.Controls.Add(grpMapSetup);
            tabRadar.Location = new Point(4, 24);
            tabRadar.Name = "tabRadar";
            tabRadar.Padding = new Padding(3);
            tabRadar.Size = new Size(1592, 872);
            tabRadar.TabIndex = 0;
            tabRadar.Text = "Radar";
            tabRadar.UseVisualStyleBackColor = true;
            // 
            // ticketsPanel
            // 
            ticketsPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ticketsPanel.BackColor = SystemColors.ScrollBar;
            ticketsPanel.BorderStyle = BorderStyle.FixedSingle;
            ticketsPanel.Location = new Point(7, 836);
            ticketsPanel.Name = "ticketsPanel";
            ticketsPanel.Size = new Size(171, 30);
            ticketsPanel.TabIndex = 12;
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
            // grpEsp
            // 
            grpEsp.Location = new Point(5, 306); 
            grpEsp.Name = "grpEsp";
            grpEsp.Size = new Size(463, 220); 
            grpEsp.TabIndex = 27;
            grpEsp.TabStop = false;
            grpEsp.Text = "ESP";
            // 
            // chkEnableEsp
            // 
            chkEnableEsp.AutoSize = true;
            chkEnableEsp.Location = new Point(10, 25);
            chkEnableEsp.Name = "chkEnableEsp";
            chkEnableEsp.Size = new Size(100, 19);
            chkEnableEsp.TabIndex = 0;
            chkEnableEsp.Text = "Enable ESP";
            chkEnableEsp.UseVisualStyleBackColor = true;
            chkEnableEsp.CheckedChanged += ChkEnableEsp_CheckedChanged;
            // 
            // trkEspMaxDistance
            // 
            trkEspMaxDistance.LargeChange = 100;
            trkEspMaxDistance.Location = new Point(10, 50);
            trkEspMaxDistance.Maximum = 1000;
            trkEspMaxDistance.Minimum = 10;
            trkEspMaxDistance.Name = "trkEspMaxDistance";
            trkEspMaxDistance.Size = new Size(200, 45);
            trkEspMaxDistance.TabIndex = 1;
            trkEspMaxDistance.TickStyle = TickStyle.None;
            trkEspMaxDistance.Value = 500; // Valeur par défaut
            trkEspMaxDistance.Scroll += TrkEspMaxDistance_Scroll;
            // 
            // lblEspMaxDistance
            // 
            lblEspMaxDistance.AutoSize = true;
            lblEspMaxDistance.Location = new Point(220, 60);
            lblEspMaxDistance.Name = "lblEspMaxDistance";
            lblEspMaxDistance.Size = new Size(100, 15);
            lblEspMaxDistance.TabIndex = 2;
            lblEspMaxDistance.Text = "Max Distance: 500m";
            // 
            // chkShowAllies
            // 
            chkShowAllies.AutoSize = true;
            chkShowAllies.Location = new Point(10, 90);
            chkShowAllies.Name = "chkShowAllies";
            chkShowAllies.Size = new Size(100, 19);
            chkShowAllies.TabIndex = 3;
            chkShowAllies.Text = "Show Allies";
            chkShowAllies.UseVisualStyleBackColor = true;
            chkShowAllies.CheckedChanged += ChkShowAllies_CheckedChanged;
            // 
            // chkEspShowNames
            // 
            chkEspShowNames.AutoSize = true;
            chkEspShowNames.Location = new Point(10, 110);
            chkEspShowNames.Name = "chkEspShowNames";
            chkEspShowNames.Size = new Size(100, 19);
            chkEspShowNames.TabIndex = 4;
            chkEspShowNames.Text = "Show Names";
            chkEspShowNames.UseVisualStyleBackColor = true;
            chkEspShowNames.CheckedChanged += ChkEspShowNames_CheckedChanged;
            // 
            // chkEspShowDistance
            // 
            chkEspShowDistance.AutoSize = true;
            chkEspShowDistance.Location = new Point(10, 130);
            chkEspShowDistance.Name = "chkEspShowDistance";
            chkEspShowDistance.Size = new Size(100, 19);
            chkEspShowDistance.TabIndex = 5;
            chkEspShowDistance.Text = "Show Distance";
            chkEspShowDistance.UseVisualStyleBackColor = true;
            chkEspShowDistance.CheckedChanged += ChkEspShowDistance_CheckedChanged;
            // 
            // chkEspShowHealth
            // 
            chkEspShowHealth.AutoSize = true;
            chkEspShowHealth.Location = new Point(10, 150);
            chkEspShowHealth.Name = "chkEspShowHealth";
            chkEspShowHealth.Size = new Size(100, 19);
            chkEspShowHealth.TabIndex = 6;
            chkEspShowHealth.Text = "Show Health";
            chkEspShowHealth.UseVisualStyleBackColor = true;
            chkEspShowHealth.CheckedChanged += ChkEspShowHealth_CheckedChanged;
            // 
            // txtEspFontSize
            // 
            txtEspFontSize.Location = new Point(110, 170);
            txtEspFontSize.Name = "txtEspFontSize";
            txtEspFontSize.Size = new Size(50, 23);
            txtEspFontSize.TabIndex = 7;
            txtEspFontSize.Text = "12"; // Valeur par défaut
            txtEspFontSize.TextChanged += TxtEspFontSize_TextChanged;
            // 
            // lblEspFontSize
            // 
            lblEspFontSize.AutoSize = true;
            lblEspFontSize.Location = new Point(10, 173);
            lblEspFontSize.Name = "lblEspFontSize";
            lblEspFontSize.Size = new Size(90, 15);
            lblEspFontSize.TabIndex = 8;
            lblEspFontSize.Text = "Font Size:";
            // 
            // txtEspColorA
            // 
            txtEspColorA.Location = new Point(50, 195);
            txtEspColorA.Name = "txtEspColorA";
            txtEspColorA.Size = new Size(40, 23);
            txtEspColorA.TabIndex = 9;
            txtEspColorA.Text = "255"; // Valeur par défaut (opaque)
            txtEspColorA.TextChanged += TxtEspColorA_TextChanged;
            // 
            // lblEspColorA
            // 
            lblEspColorA.AutoSize = true;
            lblEspColorA.Location = new Point(10, 198);
            lblEspColorA.Name = "lblEspColorA";
            lblEspColorA.Size = new Size(30, 15);
            lblEspColorA.TabIndex = 10;
            lblEspColorA.Text = "A:";
            // 
            // txtEspColorR
            // 
            txtEspColorR.Location = new Point(130, 195);
            txtEspColorR.Name = "txtEspColorR";
            txtEspColorR.Size = new Size(40, 23);
            txtEspColorR.TabIndex = 11;
            txtEspColorR.Text = "255"; // Valeur par défaut (rouge)
            txtEspColorR.TextChanged += TxtEspColorR_TextChanged;
            // 
            // lblEspColorR
            // 
            lblEspColorR.AutoSize = true;
            lblEspColorR.Location = new Point(100, 198);
            lblEspColorR.Name = "lblEspColorR";
            lblEspColorR.Size = new Size(30, 15);
            lblEspColorR.TabIndex = 12;
            lblEspColorR.Text = "R:";
            // 
            // txtEspColorG
            // 
            txtEspColorG.Location = new Point(210, 195);
            txtEspColorG.Name = "txtEspColorG";
            txtEspColorG.Size = new Size(40, 23);
            txtEspColorG.TabIndex = 13;
            txtEspColorG.Text = "255"; // Valeur par défaut (vert)
            txtEspColorG.TextChanged += TxtEspColorG_TextChanged;
            // 
            // lblEspColorG
            // 
            lblEspColorG.AutoSize = true;
            lblEspColorG.Location = new Point(180, 198);
            lblEspColorG.Name = "lblEspColorG";
            lblEspColorG.Size = new Size(30, 15);
            lblEspColorG.TabIndex = 14;
            lblEspColorG.Text = "G:";
            // 
            // txtEspColorB
            // 
            txtEspColorB.Location = new Point(290, 195);
            txtEspColorB.Name = "txtEspColorB";
            txtEspColorB.Size = new Size(40, 23);
            txtEspColorB.TabIndex = 15;
            txtEspColorB.Text = "255"; // Valeur par défaut (bleu)
            txtEspColorB.TextChanged += TxtEspColorB_TextChanged;
            // 
            // lblEspColorB
            // 
            lblEspColorB.AutoSize = true;
            lblEspColorB.Location = new Point(260, 198);
            lblEspColorB.Name = "lblEspColorB";
            lblEspColorB.Size = new Size(30, 15);
            lblEspColorB.TabIndex = 16;
            lblEspColorB.Text = "B:";
            //ESP
            grpEsp.Controls.Add(chkEnableEsp);
            grpEsp.Controls.Add(trkEspMaxDistance);
            grpEsp.Controls.Add(lblEspMaxDistance);
            grpEsp.Controls.Add(chkShowAllies);
            grpEsp.Controls.Add(chkEspShowNames);
            grpEsp.Controls.Add(chkEspShowDistance);
            grpEsp.Controls.Add(chkEspShowHealth);
            grpEsp.Controls.Add(txtEspFontSize);
            grpEsp.Controls.Add(lblEspFontSize);
            grpEsp.Controls.Add(txtEspColorA);
            grpEsp.Controls.Add(lblEspColorA);
            grpEsp.Controls.Add(txtEspColorR);
            grpEsp.Controls.Add(lblEspColorR);
            grpEsp.Controls.Add(txtEspColorG);
            grpEsp.Controls.Add(lblEspColorG);
            grpEsp.Controls.Add(txtEspColorB);
            grpEsp.Controls.Add(lblEspColorB);
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
            grpEsp.ResumeLayout(false);
            grpEsp.PerformLayout();
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
        private CheckBox chkShowEnemyDistance;
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

        // ESP
        private GroupBox grpEsp;
        private CheckBox chkEnableEsp;
        private TrackBar trkEspMaxDistance;
        private Label lblEspMaxDistance;
        private CheckBox chkShowAllies;
        private CheckBox chkEspShowNames;
        private CheckBox chkEspShowDistance;
        private CheckBox chkEspShowHealth;
        private TextBox txtEspFontSize;
        private Label lblEspFontSize;
        private TextBox txtEspColorA;
        private TextBox txtEspColorR;
        private TextBox txtEspColorG;
        private TextBox txtEspColorB;
        private Label lblEspColorA;
        private Label lblEspColorR;
        private Label lblEspColorG;
        private Label lblEspColorB;
      
        // Tickets
        private Panel ticketsPanel;

    }
}
