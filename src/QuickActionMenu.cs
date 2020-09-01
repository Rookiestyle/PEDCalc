using PluginTools;
using PluginTranslation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PEDCalc
{
	internal class QuickActionEventArgs : EventArgs
	{
		internal PEDCalcValue Value
		{
			get;
			private set;
		}

		internal QuickActionEventArgs(PEDCalcValue value)
		{
			Value = value;
		}
	}

	internal class PEDValue_QuickAction
	{
		private ToolStripTextBox m_tbValue = new ToolStripTextBox();
		private ToolStripComboBox m_cbUnit = new ToolStripComboBox();
		private ToolStripButton m_b = new ToolStripButton();

		public EventHandler<QuickActionEventArgs> ItemOrButtonClick;

		public ToolStripDropDown DropDown
		{
			private set;
			get;
		}

		public PEDValue_QuickAction(bool bHideDisableExpiryAction)
		{
			DropDown = new ToolStripDropDown();
			DropDown.LayoutStyle = ToolStripLayoutStyle.Table;
			var settings = DropDown.LayoutSettings as TableLayoutSettings;
			settings.ColumnCount = 4;

			DropDown.Items.Add(GetImage(true));
			ToolStripMenuItem tsmiQuickAction = CreateMenuItem(PluginTranslate.OptionsInactive, new PEDCalcValue(PEDC.Off));
			DropDown.Items.Add(tsmiQuickAction);
			settings.SetColumnSpan(tsmiQuickAction, 3);

			DropDown.Items.Add(GetImage(true));
			tsmiQuickAction = CreateMenuItem(PluginTranslate.OptionsInherit, new PEDCalcValue(PEDC.Inherit));
			DropDown.Items.Add(tsmiQuickAction);
			settings.SetColumnSpan(tsmiQuickAction, 3);

			DropDown.Items.Add(GetImage(false));
			tsmiQuickAction = CreateMenuItem(PluginTranslate.OptionsExpire, new PEDCalcValue(PEDC.SetExpired));
			DropDown.Items.Add(tsmiQuickAction);
			settings.SetColumnSpan(tsmiQuickAction, 3);

			if (!bHideDisableExpiryAction)
			{
				DropDown.Items.Add(GetImage(false));
				tsmiQuickAction = CreateMenuItem(KeePass.Resources.KPRes.NeverExpires, new PEDCalcValue(PEDC.SetNeverExpires));
				DropDown.Items.Add(tsmiQuickAction);
				settings.SetColumnSpan(tsmiQuickAction, 3);
			}

			for (int i = 0; i < DropDown.Items.Count; i++)
				DropDown.Items[i].AutoSize = true;

			m_tbValue.KeyDown += OnKeyDown;
			m_tbValue.Alignment = ToolStripItemAlignment.Right;
			m_tbValue.TextBoxTextAlign = HorizontalAlignment.Right;
			m_tbValue.Width = 50;
			m_tbValue.AutoSize = false;
			m_tbValue.Padding = Padding.Empty;
			m_tbValue.Margin = new Padding(0, 0, 5, 0);

			m_cbUnit.Padding = Padding.Empty;
			m_cbUnit.Margin = new Padding(0, 0, 5, 0);
			m_cbUnit.KeyDown += OnKeyDown;
			m_cbUnit.Items.AddRange(new string[] { PluginTranslate.UnitDays, PluginTranslate.UnitWeeks, PluginTranslate.UnitMonths, PluginTranslate.UnitYears });
			m_cbUnit.DropDownStyle = ComboBoxStyle.DropDownList;

			m_b.Click += OnButtonClick;
			m_b.Text = KeePass.Resources.KPRes.Ok;

			DropDown.AutoSize = true;
			DropDown.CanOverflow = true;
			DropDown.AutoClose = true;
			DropDown.DropShadowEnabled = true;

			DropDown.Padding = new Padding(5, DropDown.Padding.Top, 5, DropDown.Padding.Bottom);

			DropDown.Items.Add(GetImage(true));
			DropDown.Items.Add(m_tbValue);
			DropDown.Items.Add(m_cbUnit);
			DropDown.Items.Add(m_b);

			DropDown.Tag = this;
		}

		private ToolStripMenuItem GetImage(bool bHasImage)
		{
			return new ToolStripMenuItem()
			{
				Image = bHasImage ? PEDCalcExt.m_iconInactive : null,
			};
		}

		private ToolStripMenuItem CreateMenuItem(string sText, PEDCalcValue pcv)
		{
			ToolStripMenuItem tsmi = new ToolStripMenuItem(sText);
			tsmi.TextAlign = ContentAlignment.MiddleLeft;
			tsmi.Dock = DockStyle.Fill;
			tsmi.Click += OnItemClick;
			tsmi.Tag = pcv;
			return tsmi;
		}

		private void OnItemClick(object sender, EventArgs e)
		{
			if (ItemOrButtonClick != null)
			{
				PEDCalcValue value = new PEDCalcValue(((sender as ToolStripMenuItem).Tag as PEDCalcValue).unit);
				QuickActionEventArgs qe = new QuickActionEventArgs(value);
				ItemOrButtonClick(this, qe);
			}
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				m_b.PerformClick();
			}
		}

		private void OnButtonClick(object sender, EventArgs e)
		{
			if (ItemOrButtonClick != null)
			{
				PEDCalcValue pcv;
				PEDC unit = PEDC.Days;
				if (m_cbUnit.SelectedIndex == 1) unit = PEDC.Weeks;
				if (m_cbUnit.SelectedIndex == 2) unit = PEDC.Months;
				if (m_cbUnit.SelectedIndex == 3) unit = PEDC.Years;
				int dummy = -1;
				if (!int.TryParse(m_tbValue.Text, out dummy) || (dummy == -1))
				{
					pcv = new PEDCalcValue(PEDC.Inherit);
					PluginDebug.AddInfo("Converted expiry value", 0, "Given: " + m_tbValue.Text, "Converted: " + pcv.unit.ToString());
				}
				else if (dummy <= 0)
				{
					pcv = new PEDCalcValue(PEDC.Off);
					PluginDebug.AddInfo("Converted expiry value", 0, "Given: " + m_tbValue.Text, "Converted: " + pcv.unit.ToString());
				}
				else pcv = new PEDCalcValue(unit, dummy);

				QuickActionEventArgs qe = new QuickActionEventArgs(pcv);
				ItemOrButtonClick(this, qe);
			}
		}

		internal void SetValue(PEDCalcValue pcv)
		{
			FontStyle fs = DropDown.Items[0].Font.Style & ~FontStyle.Bold;
			Font f = new Font(DropDown.Items[0].Font, fs);
			Font fActive = new Font(DropDown.Items[0].Font, fs | FontStyle.Bold);
			DropDown.Items[1].Font = pcv.Off ? fActive : f;
			DropDown.Items[3].Font = pcv.Inherit ? fActive : f;

			if (KeePassLib.Native.NativeLib.IsUnix()) //Mono does not support changing the fontstyle
			{
				DropDown.Items[0].Text = DropDown.Items[1].Text.Replace("-> ", "");
				DropDown.Items[1].Text = DropDown.Items[3].Text.Replace("-> ", "");
				if (pcv.Off) DropDown.Items[1].Text = "-> " + DropDown.Items[1].Text;
				if (pcv.Inherit) DropDown.Items[3].Text = "-> " + DropDown.Items[3].Text;
			}
			DropDown.Items[0].Image = pcv.Off ? PEDCalcExt.m_iconActive : PEDCalcExt.m_iconInactive;
			DropDown.Items[2].Image = pcv.Inherit ? PEDCalcExt.m_iconActive : PEDCalcExt.m_iconInactive;

			if (pcv.Specific)
			{
				m_tbValue.Text = pcv.value.ToString();
				if (pcv.unit == PEDC.Days) m_cbUnit.SelectedIndex = 0;
				if (pcv.unit == PEDC.Weeks) m_cbUnit.SelectedIndex = 1;
				if (pcv.unit == PEDC.Months) m_cbUnit.SelectedIndex = 2;
				if (pcv.unit == PEDC.Years) m_cbUnit.SelectedIndex = 3;
			}
			else
			{
				if (KeePass.Program.Config.Defaults.NewEntryExpiresInDays > 0)
					m_tbValue.Text = KeePass.Program.Config.Defaults.NewEntryExpiresInDays.ToString();
				m_cbUnit.SelectedIndex = 0;
			}

			DropDown.Items[DropDown.Items.Count - 4].Image = !pcv.Off && !pcv.Inherit ? PEDCalcExt.m_iconActive : PEDCalcExt.m_iconInactive;
			m_b.CheckState = !pcv.Off && !pcv.Inherit ? CheckState.Checked : CheckState.Unchecked;
		}

		internal void SetInheritValue(PEDCalcValue pcvInherit)
		{
			DropDown.Items[3].Text = string.Format(PluginTranslate.OptionsInherit, pcvInherit.ToString(true));
		}
	}
}
