using KeePass;
using KeePass.Forms;
using KeePass.Plugins;
using KeePass.UI;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using KeePassLib.Utility;
using PluginTools;
using PluginTranslation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace PEDCalc
{
	public static class Configuration
	{
		private static string m_ConfigActive = "PEDCalc.active";
		public static readonly string Interval = "PEDCalc.interval"; //new fieldname as meanwhile days, weeks, months and years are supported
		public static bool Active
		{
			get { return Program.Config.CustomConfig.GetBool(m_ConfigActive, true); }
			set { Program.Config.CustomConfig.SetBool(m_ConfigActive, value); }
		}
		public static bool SkipRecalc = false;

		private static string m_ConfigFirstTimeDisable = "PEDCalc.FirstTimeDisable";
		public static bool FirstTimeDisable
		{
			get { return Program.Config.CustomConfig.GetBool(m_ConfigFirstTimeDisable, true); }
			set { Program.Config.CustomConfig.SetBool(m_ConfigFirstTimeDisable, value); }
		}

		public static readonly Version KeePassMultipleEntries = new Version(2, 46);
	}

	public sealed class PEDCalcExt : Plugin
	{
		private IPluginHost m_host = null;
		private ToolStripMenuItem m_menu;
		private ToolStripMenuItem m_ContextMenuEntry;
		private ToolStripMenuItem m_ContextMenuGroup;
		private ToolStripMenuItem m_MainMenuEntry;
		private ToolStripMenuItem m_MainMenuGroup;

		public static Image m_iconActive = null;
		public static Image m_iconInactive = null;

		private PEDCalcColumnProvider m_cp = new PEDCalcColumnProvider();

		PwEntryForm m_pweForm = null;
		private int m_iSelectedEntries = 0;

		public override bool Initialize(IPluginHost host)
		{
			Terminate();

			if (host == null)
				return false;
			m_host = host;
			PluginTranslate.Init(this, KeePass.Program.Translation.Properties.Iso6391Code);
			PluginTranslate.TranslationChanged += (o, e) => { PEDCalcValue.SetTranslatedUnits(); };
			Tools.DefaultCaption = PluginTranslate.PluginName;
			Tools.PluginURL = "https://github.com/rookiestyle/pedcalc/";

			m_iconActive = GfxUtil.ScaleImage(Resources.pedcalc, DpiUtil.ScaleIntX(16), DpiUtil.ScaleIntY(16));
			m_iconInactive = ToolStripRenderer.CreateDisabledImage(m_iconActive);

			PwEntry.EntryTouched += OnEntryTouched;
			GlobalWindowManager.WindowAdded += OnWindowAdded;

			Tools.OptionsFormShown += Tools_OptionsFormShown;

			PEDCValueDAO.StartLogging();

			m_host.MainWindow.FileOpened += DoMigrate;

			m_host.ColumnProviderPool.Add(m_cp);

			AddMenu();

			return true;
		}

		private void DoMigrate(object sender, FileOpenedEventArgs e)
		{
			DataMigration.CheckAndMigrate(e.Database);
		}

		private void Tools_OptionsFormShown(object sender, Tools.OptionsFormsEventArgs e)
		{
			Tools.AddPluginToOverview(this.GetType().Name);
		}

		public override void Terminate()
		{
			if (m_host == null)
				return;

			m_host.ColumnProviderPool.Remove(m_cp);

			PEDCValueDAO.EndLogging();

			Tools.OptionsFormShown -= Tools_OptionsFormShown;
			PwEntry.EntryTouched -= OnEntryTouched;
			GlobalWindowManager.WindowAdded -= OnWindowAdded;

			RemoveMenu();
			PluginDebug.SaveOrShow();

			m_host = null;
		}

		#region Menu entries
		private void AddMenu()
		{
			PluginDebug.AddInfo("Add menu", 0);
			m_menu = new ToolStripMenuItem();
			m_menu.CheckOnClick = true;
			m_menu.Checked = Configuration.Active;
			m_menu.CheckedChanged += (o, e) => ToggleActive();
			m_menu.Image = Configuration.Active ? m_iconActive : m_iconInactive;
			m_menu.Text = Configuration.Active ? PluginTranslate.Active : PluginTranslate.Inactive;
			m_host.MainWindow.ToolsMenu.DropDownItems.Add(m_menu);

			m_host.MainWindow.EntryContextMenu.Opening += OnMenuOpening;
			m_ContextMenuEntry = CreatePEDMenu(false, false);
			m_MainMenuEntry = CreatePEDMenu(false, false);

			m_host.MainWindow.GroupContextMenu.Opening += OnMenuOpening;
			m_ContextMenuGroup = CreatePEDMenu(true, false);
			m_MainMenuGroup = CreatePEDMenu(true, false);

			PluginDebug.AddInfo("Add context menu");
			m_host.MainWindow.EntryContextMenu.Items.Insert(m_host.MainWindow.EntryContextMenu.Items.Count, m_ContextMenuEntry);
			m_host.MainWindow.GroupContextMenu.Items.Insert(m_host.MainWindow.GroupContextMenu.Items.Count, m_ContextMenuGroup);

			AddEditQuickEntries();

			try
			{
				ToolStripMenuItem last = m_host.MainWindow.MainMenu.Items["m_menuGroup"] as ToolStripMenuItem;
				last.DropDownOpening += OnMenuOpening;
				last.DropDownItems.Add(m_MainMenuGroup);
				PluginDebug.AddInfo("Add group menu");

				last = m_host.MainWindow.MainMenu.Items["m_menuEntry"] as ToolStripMenuItem;
				last.DropDownOpening += OnMenuOpening;
				last.DropDownItems.Add(m_MainMenuEntry);
				PluginDebug.AddInfo("Add entry menu");
			}
			catch (Exception ex)
			{
				PluginDebug.AddError("Error adding menu", 0, ex.Message);
			}
		}

		private ToolStripMenuItem m_ContextMenuEntryQuick = null;
		private ToolStripMenuItem m_MainMenuEntryQuick = null;
		private void AddEditQuickEntries()
        {
			m_ContextMenuEntryQuick = CreatePEDMenu(false, false);
			m_MainMenuEntryQuick = CreatePEDMenu(false, false);

			ToolStripMenuItem m = Tools.FindToolStripMenuItem(m_host.MainWindow.EntryContextMenu.Items, "m_ctxEntryEditQuick", true);
			if (m != null)
			{
				m.DropDownItems.Add(m_ContextMenuEntryQuick);
				m.DropDownOpening += OnMenuOpening;
			}

			m = Tools.FindToolStripMenuItem(m_host.MainWindow.MainMenu.Items, "m_menuEntryEditQuick", true);
			if (m != null)
			{
				m.DropDownItems.Add(m_MainMenuEntryQuick);
				m.DropDownOpening += OnMenuOpening;
			}
		}

		private ToolStripMenuItem CreatePEDMenu(bool? bGroup, bool bInEntryForm)
		{
			ToolStripMenuItem tsmi = new ToolStripMenuItem(PluginTranslate.PluginName + "...");
			tsmi.Name = "PEDCALC_EntryForm_ContextMenu";
			tsmi.Image = SmallIcon;
			tsmi.DropDownOpening += OnPEDMenuOpening;
			PEDValue_QuickAction qa = new PEDValue_QuickAction(bGroup.HasValue ? bGroup.Value : false);
			if (bInEntryForm)
				qa.ItemOrButtonClick += OnPEDCalcEntryForm;
			else
				qa.ItemOrButtonClick += OnPerformPEDAction;
			tsmi.DropDown = qa.DropDown;
			tsmi.Tag = bGroup;
			return tsmi;
		}

		private void RemoveMenu()
		{
			m_host.MainWindow.ToolsMenu.DropDownItems.Remove(m_menu);
			m_host.MainWindow.EntryContextMenu.Opening -= OnMenuOpening;
			m_host.MainWindow.GroupContextMenu.Opening -= OnMenuOpening;

			m_host.MainWindow.EntryContextMenu.Items.Remove(m_ContextMenuEntry);
			m_host.MainWindow.GroupContextMenu.Items.Remove(m_ContextMenuGroup);

			try
			{
				(m_host.MainWindow.MainMenu.Items["m_menuEntry"] as ToolStripMenuItem).DropDownOpening -= OnMenuOpening;
				(m_host.MainWindow.MainMenu.Items["m_menuGroup"] as ToolStripMenuItem).DropDownOpening -= OnMenuOpening;
				m_MainMenuEntry.Owner.Items.Remove(m_MainMenuEntry);
				m_MainMenuGroup.Owner.Items.Remove(m_MainMenuGroup);
			}
			catch (Exception) { }
		}

		private void OnMenuOpening(object sender, EventArgs e)
		{
			m_ContextMenuGroup.Enabled = m_host.MainWindow.GetSelectedGroup() != null;
			m_MainMenuGroup.Enabled = m_ContextMenuGroup.Enabled;
			m_ContextMenuEntry.Enabled = m_host.MainWindow.GetSelectedEntriesCount() >= 1;
			m_MainMenuEntry.Enabled = m_ContextMenuEntry.Enabled;
			m_ContextMenuEntryQuick.Enabled= m_MainMenuEntryQuick.Enabled = m_ContextMenuEntry.Enabled;
		}

		private void OnPEDMenuOpening(object sender, EventArgs e)
		{
			ToolStripMenuItem tsmi = sender as ToolStripMenuItem;
			if (tsmi == null) return;
			if (tsmi.DropDown == null) return;
			PEDValue_QuickAction pqaActions = tsmi.DropDown.Tag as PEDValue_QuickAction;
			if (pqaActions == null) return;
			bool? bGroupOrEntry = (bool?)tsmi.Tag;
			if (bGroupOrEntry.HasValue && !bGroupOrEntry.Value)
			//if ((tsmi == m_ContextMenuEntry) || (tsmi == m_MainMenuEntry) || (tsmi == m_ContextMenuEntryQuick) || (tsmi == m_MainMenuEntryQuick))
			{
				PwEntry[] pe = m_host.MainWindow.GetSelectedEntries();
				PEDCalcValue pcvInherit = pe[0].GetPEDValueInherit();
				pqaActions.SetInheritValue(pcvInherit);
				pqaActions.SetValue(pe[0].GetPEDValue(false));
			}
			else if (bGroupOrEntry.HasValue && bGroupOrEntry.Value)
			//else if ((tsmi == m_ContextMenuGroup) || (tsmi == m_MainMenuGroup))
			{
				PwGroup pg = m_host.MainWindow.GetSelectedGroup();
				PEDCalcValue pcvInherit = pg.GetPEDValueInherit();
				pqaActions.SetInheritValue(pcvInherit);
				pqaActions.SetValue(pg.GetPEDValue(false));
			}
			else if (m_pweForm != null)
			{
				PwEntry pe = m_pweForm.EntryRef;
				StringDictionaryEx dCustomData = (StringDictionaryEx)Tools.GetField("m_sdCustomData", m_pweForm);
				PEDCalcValue currentValue;
				if (dCustomData != null) currentValue = PEDCalcValue.ConvertFromString(dCustomData.Get(Configuration.Interval));
				else currentValue = PEDCalcValue.ConvertFromString(pe.ReadPEDCString());
				if (currentValue == null) currentValue = pe.GetPEDValue(false);
				pqaActions.SetInheritValue(pe.GetPEDValueInherit());
				pqaActions.SetValue(currentValue);
			}
		}

		private void ToggleActive()
		{
			if (Configuration.Active && Configuration.FirstTimeDisable)
			{
				Configuration.FirstTimeDisable = false;
				if (Tools.AskYesNo(string.Format(PluginTranslate.CheckDisable, PluginTranslate.PluginName)) == DialogResult.No) return;
			}
			Configuration.Active = !Configuration.Active;
			m_menu.Image = Configuration.Active ? m_iconActive : m_iconInactive;
			m_menu.Text = Configuration.Active ? PluginTranslate.Active : PluginTranslate.Inactive;
		}
		#endregion

		#region Entry and group handling
		private void OnEntryTouched(object sender, ObjectTouchedEventArgs e)
		{
			if (Configuration.SkipRecalc || !Configuration.Active)
			{
				Configuration.SkipRecalc = false;
				return;
			}

			if (!e.Modified) return;

			(e.Object as PwEntry).RecalcExpiry();
		}

		private void OnPerformPEDAction(object sender, QuickActionEventArgs e)
		{
			ToolStripDropDown tsdd = (sender as PEDValue_QuickAction).DropDown;
			if (tsdd == null) return;

			bool bEntry = (m_ContextMenuEntry.DropDown == tsdd) || (m_MainMenuEntry.DropDown == tsdd) || (m_ContextMenuEntryQuick.DropDown == tsdd) || (m_MainMenuEntryQuick.DropDown == tsdd);
			bool bGroup = (m_ContextMenuGroup.DropDown == tsdd) || (m_MainMenuGroup.DropDown == tsdd);

			if (!bEntry && !bGroup) return;
			//PEDCalcValue pcv = tsmiAction != null ? tsmiAction.Tag as PEDCalcValue : (sender as PEDValue_QuickAction).PEDValue;

			if (bGroup)
			{
				if (e.Value.unit == PEDC.SetNeverExpires) e.Value.unit = PEDC.Off;
				PwGroup pg = m_host.MainWindow.GetSelectedGroup();
				if (e.Value.unit == PEDC.SetExpired)
				{
					bool bExpireAll = CheckExpireAll(pg);
					pg.Expire(bExpireAll);
				}
				else
				{
					pg.SavePEDCString(e.Value);
					if (Configuration.Active && !e.Value.Off && (Tools.AskYesNo(PluginTranslate.AskRecalcAll) == DialogResult.Yes))
						pg.RecalcExpiry();
					pg.Touch(true, false);
				}
				Tools.RefreshEntriesList(true);
			}
			else if (bEntry)
			{
				List<PwEntry> lEntries = m_host.MainWindow.GetSelectedEntries().ToList();
				if (e.Value.unit == PEDC.SetExpired)
				{
					bool bExpireAll = CheckExpireAll(lEntries);
					foreach (PwEntry entry in lEntries)
						entry.Expire(bExpireAll);
				}
				else
				{
					if (e.Value.unit != PEDC.SetNeverExpires)
					{
						foreach (PwEntry entry in lEntries) entry.SavePEDCString(e.Value);
						if (Configuration.Active && !e.Value.Off && (Tools.AskYesNo(lEntries.Count() > 1 ? PluginTranslate.AskRecalcAll : PluginTranslate.AskRecalcSingle) == DialogResult.Yes))
						{
							foreach (PwEntry entry in lEntries)
							{
								entry.Expires = true;
								entry.RecalcExpiry(true);
							}
						}
					}
					foreach (PwEntry entry in lEntries)
					{
						Configuration.SkipRecalc = true;
						if (e.Value.unit == PEDC.SetNeverExpires) entry.Expires = false;
						entry.Touch(true, false);
					}
				}
				Tools.RefreshEntriesList(true);
			}
		}

		private bool CheckExpireAll(PwGroup pg)
		{
			return CheckExpireAll(pg.GetEntries(true).ToList());
		}

		private bool CheckExpireAll(List<PwEntry> lEntries)
		{
			if (lEntries.Count < 2) return true; //Expire w/out asking, if only one entry is selected

			int iNotExpiringYet = lEntries.Where(x => !x.Expires).Count();
			if (iNotExpiringYet == 0) return false; //No entry w/out expiry, no need to ask

			if (iNotExpiringYet == lEntries.Count) return true; //Expire w/out asking, if ALL selected entries have no expiry set

			return Tools.AskYesNo(string.Format(PluginTranslate.ExpireAllEntriesQuestion,
				PluginTranslate.OptionsExpire, lEntries.Count, iNotExpiringYet)) == DialogResult.No;

		}
		#endregion

		#region Entry form hooks
		private void OnWindowAdded(object sender, GwmWindowEventArgs e)
		{
			if (!Configuration.Active) return;
			if (!(e.Form is PwEntryForm)) return;

			//Don't set m_pweForm now
			//User might open an older version and m_pweForm will be overwritten by that
			//Also other plugins might open a 2nd PwEntryForm (you never know...)
			e.Form.Shown += OnFormShown;
		}

		private void OnFormShown(object sender, EventArgs e)
		{
			PwEditMode m = PwEditMode.Invalid;
			(sender as Form).Activated -= OnFormShown;
			PropertyInfo pEditMode = typeof(PwEntryForm).GetProperty("EditModeEx");
			if (pEditMode != null) //will work starting with KeePass 2.41, preferred way as it's a public attribute
				m = (PwEditMode)pEditMode.GetValue(sender, null);
			else // try reading private field
				m = (PwEditMode)Tools.GetField("m_pwEditMode", sender);
			PluginDebug.AddSuccess("Entryform shown, editmode: " + m.ToString(), 0);
			if ((m != PwEditMode.AddNewEntry) && (m != PwEditMode.EditExistingEntry)) return;
			m_pweForm = sender as PwEntryForm;
			m_pweForm.Activated += OnFormShown;
			m_pweForm.FormClosed += OnFormClosed;
			m_pweForm.EntrySaving += OnEntrySaving;
			CustomContextMenuStripEx ctx = (CustomContextMenuStripEx)Tools.GetField("m_ctxDefaultTimes", m_pweForm);
			if (ctx != null)
			{
				if (ctx.Enabled && !ctx.Items.ContainsKey("PEDCALC_EntryForm_ContextMenu"))
				{

					ctx.Items.Add(new ToolStripSeparator());
					ToolStripMenuItem tsmiPED = CreatePEDMenu(null, true);
					ctx.Items.Add(tsmiPED);
					PluginDebug.AddSuccess("Found m_ctxDefaultTimes", 0);
				}
			}
			else
				PluginDebug.AddError("Could not find m_ctxDefaultTimes", 0);

			PEDCalcValue ped = m_pweForm.EntryRef.GetPEDValue(true);
			CheckBox cbExpires = (CheckBox)Tools.GetControl("m_cbExpires", m_pweForm);
			DateTimePicker dtExpireDate = (DateTimePicker)Tools.GetControl("m_dtExpireDateTime", m_pweForm);
			DateTime expiry = ped.NewExpiryDateUtc.ToLocalTime();
			if (m == PwEditMode.EditExistingEntry)
			{
				cbExpires.CheckedChanged += (o, e1) => CheckShowNewExpireDate();
				dtExpireDate.ValueChanged += (o, e1) => CheckShowNewExpireDate();
				SecureTextBoxEx password = (SecureTextBoxEx)Tools.GetControl("m_tbPassword", m_pweForm);
				password.TextChanged += (o, e1) => CheckShowNewExpireDate();
				Label lNewExpireDate = new Label();
				lNewExpireDate.Name = "PEDCalc_NewExpireDate";
				string sDate = string.Empty;
				m_iSelectedEntries = (int)m_host.MainWindow.GetSelectedEntriesCount();
				if ((Tools.KeePassVersion >= Configuration.KeePassMultipleEntries) && (m_iSelectedEntries > 1))
				{
					PropertyInfo piMultiple = typeof(KeePass.Resources.KPRes).GetProperty("MultipleValues");
					if (piMultiple != null) sDate = piMultiple.GetValue(null, null) as string;
					else sDate = "?";
				}
				else if (dtExpireDate.Format == DateTimePickerFormat.Long)
					sDate = expiry.ToLongDateString();
				else if (dtExpireDate.Format == DateTimePickerFormat.Short)
					sDate = expiry.ToShortDateString();
				else if (dtExpireDate.Format == DateTimePickerFormat.Time)
					sDate = expiry.ToLongTimeString();
				else
					sDate = expiry.ToString(dtExpireDate.CustomFormat);
				lNewExpireDate.Text = PluginTranslate.PluginName + ": " + sDate;
				lNewExpireDate.Left = dtExpireDate.Left;
				lNewExpireDate.Top = dtExpireDate.Top + dtExpireDate.Height + 2;
				lNewExpireDate.Width = dtExpireDate.Width;
				lNewExpireDate.AutoSize = true;
				ToolTip tt = new ToolTip();
				tt.ToolTipTitle = PluginTranslate.PluginName;
				tt.ToolTipIcon = ToolTipIcon.Info;
				tt.SetToolTip(lNewExpireDate, PluginTranslate.NewExpiryDateTooltip);
				dtExpireDate.Parent.Controls.Add(lNewExpireDate);
				int h = dtExpireDate.Parent.ClientSize.Height;
				if (h < lNewExpireDate.Top + lNewExpireDate.Height + 2)
					h = lNewExpireDate.Top + lNewExpireDate.Height + 2 - h;
				else
					h = 0;
				try
				{
					dtExpireDate.Parent.Parent.Height += h;
				}
				catch { }
				CheckShowNewExpireDate();
			}

			if (m == PwEditMode.AddNewEntry)
			{
				if (ped.Off) return;
				if ((cbExpires == null) || (dtExpireDate == null))
				{
					Tools.ShowError(string.Format(PluginTranslate.ErrorInitExpiryDate, expiry.ToString()));
					return;
				}
				m_pweForm.EntryRef.ExpiryTime = dtExpireDate.Value = expiry;
				m_pweForm.EntryRef.Expires = cbExpires.Checked = true;
				PwEntry peInitialEntry = (PwEntry)Tools.GetField("m_pwInitialEntry", m_pweForm);
				if (peInitialEntry != null)
				{
					peInitialEntry.Expires = true;
					peInitialEntry.ExpiryTime = expiry.ToUniversalTime();
				}
			}
		}

        private void CheckShowNewExpireDate()
		{
			if (m_pweForm == null) return;

			Label lNewExpireDate = (Label)Tools.GetControl("PEDCalc_NewExpireDate", m_pweForm);
			if (lNewExpireDate == null) return;
			lNewExpireDate.Visible = false;

			CheckBox cbExpires = (CheckBox)Tools.GetControl("m_cbExpires", m_pweForm);
			PEDCalcValue ped = m_pweForm.EntryRef.GetPEDValue(true);

			//Automatic recalculation required?
			if (ped.Off && (m_iSelectedEntries == 1)) return;

			//Entry does not expire => Don't show calculated expiry date
			if ((cbExpires == null) || !cbExpires.Checked) return;

			//Check for manual change of expiry date
			DateTimePicker dtExpireDate = (DateTimePicker)Tools.GetControl("m_dtExpireDateTime", m_pweForm);
			if ((dtExpireDate.Value != m_pweForm.EntryRef.ExpiryTime.ToLocalTime()) && m_pweForm.EntryRef.Expires) return;

			//Check for change of password
			SecureTextBoxEx password = (SecureTextBoxEx)Tools.GetControl("m_tbPassword", m_pweForm);
			ProtectedString psOldPw = m_pweForm.EntryRef.Strings.GetSafe(PwDefs.PasswordField);
			if ((password == null) || password.TextEx.Equals(psOldPw, false)) return;

			lNewExpireDate.Visible = true;
		}

		private void OnEntrySaving(object sender, KeePass.Util.CancellableOperationEventArgs e)
		{
			if (!Configuration.Active) return;
			//try reading fields from password entry form
			//checking and update the expiry date this way will save
			//us from creating one more backup just for 
			//changing the expiry date in the "Touched" event
			if (m_pweForm == null) return;

			PEDCalcValue days = m_pweForm.EntryRef.GetPEDValue(true);
			if (!days.Specific) return; //Nothing to do

			CheckBox expires = (CheckBox)Tools.GetControl("m_cbExpires", m_pweForm);
			if (expires == null) return; //read failed
			if (!expires.Checked) return; //entry does not expire (any longer)
			DateTimePicker expiryDate = (DateTimePicker)Tools.GetControl("m_dtExpireDateTime", m_pweForm);
			if (expiryDate == null) return;//read failed
			if ((TimeUtil.ToUtc(expiryDate.Value, false) != m_pweForm.EntryRef.ExpiryTime) && m_pweForm.EntryRef.Expires) return; //expiry date was already changed by the user

			SecureTextBoxEx password = (SecureTextBoxEx)Tools.GetControl("m_tbPassword", m_pweForm);
			if (password == null) return; //read failed;
			byte[] pw_new = password.TextEx.ReadUtf8();
			byte[] pw_old = m_pweForm.EntryRef.Strings.GetSafe(PwDefs.PasswordField).ReadUtf8();
			if (MemUtil.ArraysEqual(pw_new, pw_old)) return; //password was not changed

			//calculate new expiry date and write back to form field
			if (expiryDate.Value.Kind == DateTimeKind.Local)
				expiryDate.Value = days.NewExpiryDateUtc.ToLocalTime();
			else
				expiryDate.Value = days.NewExpiryDateUtc;
		}

		private void OnPEDCalcEntryForm(object sender, QuickActionEventArgs e)
		{
			ToolStripDropDown tsdd = (sender as PEDValue_QuickAction).DropDown;
			if (tsdd == null) return;

			StringDictionaryEx dCustomData = null;
			dCustomData = (StringDictionaryEx)Tools.GetField("m_sdCustomData", m_pweForm);
			ListView lvCustomData = (ListView)Tools.GetControl("m_lvCustomData", m_pweForm);
			if (dCustomData != null)
			{
				if (e.Value.Inherit)
				{
					dCustomData.Remove(Configuration.Interval);
					if (lvCustomData != null)
					{
						for (int i = 0; i < lvCustomData.Items.Count; i++)
						{
							if (lvCustomData.Items[i].Text == Configuration.Interval)
							{
								lvCustomData.Items.RemoveAt(i);
								break;
							}
						}
					}
				}
				else if (e.Value.unit != PEDC.SetExpired)
				{
					dCustomData.Set(Configuration.Interval, e.Value.ToString());
					if (lvCustomData != null)
					{
						for (int i = 0; i < lvCustomData.Items.Count; i++)
						{
							if (lvCustomData.Items[i].Text == Configuration.Interval)
							{
								lvCustomData.Items.RemoveAt(i);
								break;
							}
						}
						ListViewItem li = lvCustomData.Items.Add(Configuration.Interval);
						li.SubItems.Add(Configuration.Interval);
						li.SubItems[1].Text = e.Value.ToString();
					}
				}
			}
			if (e.Value.Off) return;
			ExpiryControlGroup ecg = (ExpiryControlGroup)Tools.GetField("m_cgExpiry", m_pweForm);
			PEDCalcValue pcv = e.Value;
			if (!e.Value.Specific)
				pcv = m_pweForm.EntryRef.GetPEDValueInherit();
			if (!pcv.Off)
			{
				ecg.Checked = true;
				if (e.Value.unit == PEDC.SetExpired)
					ecg.Value = PEDCalcValue.UnixStart;
				else
					ecg.Value = pcv.NewExpiryDateUtc;
			}
		}

		private void OnFormClosed(object sender, EventArgs e)
		{
			m_pweForm = null;
		}
		#endregion

		public override string UpdateUrl
		{
			get { return "https://raw.githubusercontent.com/rookiestyle/pedcalc/master/version.info"; }
		}

		public override Image SmallIcon
		{
			get { return m_iconActive; }
		}
	}
}
