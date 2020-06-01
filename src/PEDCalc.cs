using KeePassLib;
using PluginTools;
using PluginTranslation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PEDCalc
{
	public enum PEDC
	{
		Inherit,
		Off,
		Days,
		Weeks,
		Months,
		Years,
		SetExpired,
		SetNeverExpires,
	}

	public class PEDCalcValue
	{
		public static DateTime UnixStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
		private int m_value = 30;
		public int value
		{
			get { return m_value; }
			set { SetValue(m_unit, value, true); }
		}

		private PEDC m_unit = PEDC.Inherit;
		public PEDC unit
		{
			get { return m_unit; }
			set { SetValue(value, m_value, false); }
		}
		public DateTime NewExpiryDateUtc { get; private set; }

		public bool Inherit { get { return m_unit == PEDC.Inherit; } }
		public bool Off { get { return m_unit == PEDC.Off; } }
		public bool Specific { get { return !Inherit && !Off; } }

		public PEDCalcValue(PEDC unit)
		{
			m_unit = unit;
			SetValue(m_unit, m_value, false);
		}

		public PEDCalcValue(PEDC unit, int value)
		{
			m_unit = unit;
			m_value = value;
			SetValue(m_unit, m_value, false, true);
		}

		public override string ToString()
		{
			if (!Specific) return m_unit.ToString();
			return string.Format("{0} {1}", value, m_unit.ToString());
		}

		public string ToString(bool bLocalized)
		{
			if (!bLocalized) return ToString();
			string sUnit = string.Empty;
			switch (unit)
			{
				case PEDC.Days: sUnit = PluginTranslate.UnitDays; break;
				case PEDC.Weeks: sUnit = PluginTranslate.UnitWeeks; break;
				case PEDC.Months: sUnit = PluginTranslate.UnitMonths; break;
				case PEDC.Years: sUnit = PluginTranslate.UnitYears; break;
				case PEDC.Off: sUnit = PluginTranslate.OptionsInactive; break;
				case PEDC.Inherit: sUnit = PluginTranslate.InheritInherit; break;
			}
			if (!Specific) return sUnit;
			return string.Format("{0} {1}", value, sUnit);
		}

		public static PEDCalcValue ConvertFromString(string stringValue)
		{
			PEDCalcValue result = new PEDCalcValue(PEDC.Inherit);
			if (string.IsNullOrEmpty(stringValue)) return result;
			string[] s = stringValue.Split(new char[] { ' ' });
			int val = 0;
			if (s.Count() < 1) return result;
			if (!int.TryParse(s[0], out val))
			{
				try
				{
					PEDC unit = (PEDC)Enum.Parse(typeof(PEDC), s[0], true);
					result.unit = unit;
				}
				catch { }
				return result;
			}
			result.value = val;
			try
			{
				PEDC unit = (PEDC)Enum.Parse(typeof(PEDC), s[1], true);
				result.unit = unit;
			}
			catch { }
			return result;
		}

		private void SetValue(PEDC newUnit, int newValue, bool ValueChanged)
		{
			SetValue(newUnit, newValue, ValueChanged, false);
		}

		private void SetValue(PEDC newUnit, int newValue, bool ValueChanged, bool Force)
		{
			if (ValueChanged && !Specific) newUnit = PEDC.Days;
			if (!Force && (m_unit == newUnit) && (m_value == newValue)) return;
			m_unit = newUnit;
			m_value = newValue;
			if (m_unit == PEDC.Inherit) m_value = -1;
			if (m_unit == PEDC.Off) m_value = 0;
			if (m_value == -1) m_unit = PEDC.Inherit;
			if (m_value == 0) m_unit = PEDC.Off;
			if ((m_unit == PEDC.Inherit) || (m_unit == PEDC.Off))
			{
				NewExpiryDateUtc = UnixStart.ToUniversalTime();
				return;
			}
			double days = ConvertToDays();
			NewExpiryDateUtc = DateTime.Now.AddDays(days + 1);
			NewExpiryDateUtc = NewExpiryDateUtc.Date;
			TimeSpan x = new TimeSpan(0, 0, 1);
			NewExpiryDateUtc = NewExpiryDateUtc.Subtract(new TimeSpan(0, 0, 1));
			NewExpiryDateUtc = NewExpiryDateUtc.ToUniversalTime();
		}

		private double ConvertToDays()
		{
			if (unit == PEDC.Days)
				return value;
			if (unit == PEDC.Weeks)
				return value * 7;
			if (unit == PEDC.Months)
				return (DateTime.Now.AddMonths(value) - DateTime.Now).TotalDays;
			if (unit == PEDC.Years)
				return (DateTime.Now.AddYears(value) - DateTime.Now).TotalDays;
			return 0;
		}

		public override bool Equals(object obj)
		{
			string s1 = string.Empty;
			string s2 = string.Empty;
			try
			{
				s1 = this.ToString();
			}
			catch { }
			try
			{
				s2 = obj != null ? obj.ToString() : string.Empty;
			}
			catch { }
			return s1 == s2;
		}

		public static bool operator ==(PEDCalcValue p1, PEDCalcValue p2)
		{
			if (ReferenceEquals(p1, null))
				return ReferenceEquals(p2, null);
			return p1.Equals(p2);
		}

		public static bool operator !=(PEDCalcValue p1, PEDCalcValue p2)
		{
			return !(p1 == p2);
		}

		public override int GetHashCode()
		{
			return value.GetHashCode() ^ unit.GetHashCode();
		}
	}

	public static class PEDCValueDAO //data access object to buffer PEDCValues
	{
		private struct PEDCValueEntry
		{
			internal PEDCalcValue value;
			internal PEDCalcValue valueinherit;
		}
		private static Dictionary<PwEntry, string> m_dPEDValuesString = new Dictionary<PwEntry, string>();
		private static Dictionary<PwEntry, PEDCValueEntry> m_dPEDValues = new Dictionary<PwEntry, PEDCValueEntry>();

		public static void StartLogging()
		{
			PwGroup.GroupTouched += OnGroupTouched;
			PwEntry.EntryTouched += OnEntryTouched;
		}

		public static void EndLogging(bool bClear)
		{
			PwGroup.GroupTouched -= OnGroupTouched;
			PwEntry.EntryTouched -= OnEntryTouched;

			if (!bClear) return;
			m_dPEDValuesString.Clear();
			m_dPEDValues.Clear();
		}

		public static void Invalidate(PwEntry pe)
		{
			m_dPEDValues.Remove(pe);
			m_dPEDValuesString.Remove(pe);
		}

		public static void Invalidate(PwGroup pg)
		{
			List<PwEntry> lRemove = m_dPEDValuesString.Keys.ToList();
			lRemove.RemoveAll(x => !x.IsContainedIn(pg));
			foreach (PwEntry pe in lRemove)
				Invalidate(pe);
		}

		public static PEDCalcValue GetPEDCValue(PwEntry pe, bool recursion)
		{
			if (!m_dPEDValues.ContainsKey(pe))
			{
				string days_string = pe.ReadPEDCString();
				PEDCValueEntry pve = new PEDCValueEntry();
				pve.value = PEDCalcValue.ConvertFromString(days_string);

				if (pve.value.Inherit && (pe.ParentGroup != null))
					pve.valueinherit = pe.ParentGroup.GetPEDValue(true);
				else
					pve.valueinherit = new PEDCalcValue(PEDC.Off);
				m_dPEDValues[pe] = pve;
				PluginDebug.AddInfo("Add PEDCValues to buffer", 0,
					"Entry: " + pe.Uuid.ToString() + " / " + pe.Strings.ReadSafe(PwDefs.TitleField),
					"Value: " + pve.value.ToString(),
					"Value inherited: " + pve.valueinherit.ToString());
			}
			if (recursion && m_dPEDValues[pe].value.Inherit) return m_dPEDValues[pe].valueinherit;
			return m_dPEDValues[pe].value;
		}

		public static string GetPEDCValueString(PwEntry pe)
		{
			if (!m_dPEDValuesString.ContainsKey(pe))
			{
				PEDCalcValue pcv_entry = pe.GetPEDValue(false);
				bool bInherit = pcv_entry.Inherit;
				if (bInherit) pcv_entry = pe.GetPEDValue(true);
				string sUnit = pcv_entry.ToString(true);
				m_dPEDValuesString[pe] = sUnit + (bInherit ? "*" : string.Empty);
				PluginDebug.AddInfo("Add PEDCValue-string to buffer", 0,
					"Entry: " + pe.Uuid.ToString() + " / " + pe.Strings.ReadSafe(PwDefs.TitleField),
					"Value: " + m_dPEDValuesString[pe]);
			}
			return m_dPEDValuesString[pe];
		}

		private static void OnEntryTouched(object sender, ObjectTouchedEventArgs e)
		{
			// !e.Modified = Entry was not modified => nothing to do
			if (!e.Modified) return;
			PwEntry pe = e.Object as PwEntry;
			if (pe == null) return;
			Invalidate(pe);
		}

		private static void OnGroupTouched(object sender, ObjectTouchedEventArgs e)
		{
			// !e.Modified = Group was not modified => nothing to do
			// e.ParentsTouched => Be lazy and assume no PEDCValue was changed (we don't touch parents)
			if (!e.Modified || e.ParentsTouched) return;
			PwGroup pg = e.Object as PwGroup;
			if (pg == null) return;
			Invalidate(pg);
		}
	}
}
