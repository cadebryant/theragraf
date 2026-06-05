namespace Theragraf.Core.Models;

public enum ClinicalSetting
{
    /// <summary>Standard clinic or private practice (most common; CMS 8-minute rule applies).</summary>
    Outpatient,

    /// <summary>Inpatient acute-care hospital (per-diem billing; timed CPT units may not apply).</summary>
    Inpatient,

    /// <summary>Skilled Nursing Facility (Medicare Part A per-diem; timed CPT units not separately billable).</summary>
    SkilledNursingFacility,

    /// <summary>Home health visit (benefit-period billing; follows home-health coverage rules).</summary>
    HomeHealth,

    /// <summary>School-based services under IDEA/504 (not insurance-billed; state/district billing applies).</summary>
    SchoolBased,

    /// <summary>Early Intervention program (birth–3; state program billing; CPT codes may differ by state).</summary>
    EarlyIntervention,

    /// <summary>Telehealth / virtual visit (may require GT/95 modifier; payer policy varies widely).</summary>
    Telehealth
}
