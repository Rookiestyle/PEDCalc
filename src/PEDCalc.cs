using System;
using System.Collections.Generic;
using System.Linq;
using KeePassLib;
using PluginTools;
using PluginTranslation;

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
    Hours,
    SetExpired,
    SetNeverExpires,
  }

  public class PEDCalcValue
  {
    private static char[] m_Sep = new char[1] { ' ' };
    public static DateTime UnixStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
    private int m_value = 30;
    public int value
    {
      get { return m_value; }
      set { SetValue(m_unit, value, true, false); }
    }

    private PEDC m_unit = PEDC.Inherit;
    public PEDC unit
    {
      get { return m_unit; }
      set { SetValue(value, m_value, false, false); }
    }
    public DateTime NewExpiryDateUtc { get; private set; }

    public bool Inherit { get { return m_unit == PEDC.Inherit; } }
    public bool Off { get { return m_unit == PEDC.Off; } }
    public bool Specific { get { return !Inherit && !Off; } }

    static PEDCalcValue()
    {
      SetTranslatedUnits();
    }

    public static void SetTranslatedUnits()
    {
      var PEDCItems = Enum.GetValues(typeof(PEDC)).Cast<PEDC>();
      foreach (var i in PEDCItems)
      {
        if (i == PEDC.Inherit || i == PEDC.Off || i == PEDC.SetExpired || i == PEDC.SetNeverExpires) continue;
        string sName = i.ToString();
        System.Reflection.FieldInfo fiString = typeof(PluginTranslate).GetField("Unit" + sName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        if (fiString != null) sName = fiString.GetValue(null) as string;
        m_dTranslatedUnits[i] = sName;
      }
    }

    public PEDCalcValue(PEDC unit)
    {
      m_unit = unit;
      SetValue(m_unit, m_value, false, false);
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
        case PEDC.SetExpired:
        case PEDC.SetNeverExpires: break;
        case PEDC.Off: sUnit = PluginTranslate.OptionsInactive; break;
        case PEDC.Inherit: sUnit = PluginTranslate.InheritInherit; break;
        default: sUnit = GetTranslatedUnit(unit); break;
      }
      if (!Specific) return sUnit;
      return string.Format("{0} {1}", value, sUnit);
    }

    private static Dictionary<PEDC, string> m_dTranslatedUnits = new Dictionary<PEDC, string>();
    public static string GetTranslatedUnit(PEDC unit)
    {
      string sTranslated = string.Empty;
      if (m_dTranslatedUnits.TryGetValue(unit, out sTranslated)) return sTranslated;
      sTranslated = unit.ToString();
      m_dTranslatedUnits[unit] = sTranslated;
      return sTranslated;
    }

    public static PEDCalcValue ConvertFromString(string stringValue)
    {
      PEDCalcValue result = new PEDCalcValue(PEDC.Inherit);
      if (string.IsNullOrEmpty(stringValue)) return result;
      string[] s = stringValue.Split(m_Sep);
      int val = 0;
      if (s.Count() < 1) return result;
      if (!int.TryParse(s[0], out val))
      {
        //Ok, it's nothing like '5 days'
        //So it better is one of the defined values for enum PEDC
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
      if (m_unit == PEDC.Hours)
      {
        NewExpiryDateUtc = DateTime.Now.AddHours(days).ToUniversalTime();
        return;
      }
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
      if (unit == PEDC.Hours)
        return value; //Do not convert to hours
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
      if (ReferenceEquals(p1, null)) return ReferenceEquals(p2, null);
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

    //Use PwEntry instead of PwUuid
    //Using PwEntry sometimes is buggy, e. g. when changing an entry in PwEntryForm but not saving the changes
    //When checking for updates, search by PwUuid
    //PwEntry required to invalidate groups (all entries within this group)
    private static Dictionary<PwEntry, PEDCValueEntry> m_dPEDValues = new Dictionary<PwEntry, PEDCValueEntry>();
    //Use PwUuid
    private static Dictionary<PwUuid, string> m_dPEDValuesString = new Dictionary<PwUuid, string>();

    public static void StartLogging()
    {
      PwGroup.GroupTouched += OnObjectTouched;
      PwEntry.EntryTouched += OnObjectTouched;
    }

    public static void EndLogging()
    {
      PwGroup.GroupTouched -= OnObjectTouched;
      PwEntry.EntryTouched -= OnObjectTouched;

      m_dPEDValuesString.Clear();
      m_dPEDValues.Clear();
    }

    public static void Invalidate(PwEntry pe)
    {
      m_dPEDValuesString.Remove(pe.Uuid);
      m_dPEDValues.Remove(pe);
      return;
      List<PwEntry> lRemove = m_dPEDValues.Keys.ToList().FindAll(x => x.Uuid.Equals(pe.Uuid));
      if (lRemove == null) return;
      foreach (PwEntry peRemove in lRemove) m_dPEDValues.Remove(peRemove);
    }

    public static void Invalidate(PwGroup pg)
    {
      List<PwEntry> lRemove = m_dPEDValues.Keys.ToList();
      lRemove.RemoveAll(x => !x.IsContainedIn(pg));
      foreach (PwEntry pe in lRemove) Invalidate(pe);
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
          "Entry: " + pe.Uuid.ToHexString() + " / " + pe.Strings.ReadSafe(PwDefs.TitleField),
          "Value: " + pve.value.ToString(),
          "Value inherited: " + pve.valueinherit.ToString());
      }
      if (recursion && m_dPEDValues[pe].value.Inherit) return m_dPEDValues[pe].valueinherit;
      return m_dPEDValues[pe].value;
    }

    public static string GetPEDCValueString(PwEntry pe)
    {
      if (!m_dPEDValuesString.ContainsKey(pe.Uuid))
      {
        PEDCalcValue pcv_entry = pe.GetPEDValue(false);
        bool bInherit = pcv_entry.Inherit;
        if (bInherit) pcv_entry = pe.GetPEDValue(true);
        string sUnit = pcv_entry.ToString(true);
        m_dPEDValuesString[pe.Uuid] = sUnit + (bInherit ? "*" : string.Empty);
        PluginDebug.AddInfo("Add PEDCValue-string to buffer", 0,
          "Entry: " + pe.Uuid.ToHexString() + " / " + pe.Strings.ReadSafe(PwDefs.TitleField),
          "Value: " + m_dPEDValuesString[pe.Uuid]);
      }
      return m_dPEDValuesString[pe.Uuid];
    }

    private static void OnObjectTouched(object sender, ObjectTouchedEventArgs e)
    {
      // !e.Modified = Entry was not modified => nothing to do
      if (!e.Modified) return;
      if (e.Object is PwEntry) Invalidate(e.Object as PwEntry);
      if (e.Object is PwGroup) Invalidate(e.Object as PwGroup);
    }
  }
}
