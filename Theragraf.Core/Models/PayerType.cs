namespace Theragraf.Core.Models;

public enum PayerType
{
    /// <summary>Medicare (traditional FFS; CMS 8-minute rule applies to timed codes).</summary>
    Medicare,

    /// <summary>Medicare Advantage (same CPT codes as FFS but payer policies and caps vary by plan).</summary>
    MedicareAdvantage,

    /// <summary>Medicaid (state-specific rates and covered codes; prior auth requirements vary).</summary>
    Medicaid,

    /// <summary>Commercial / private insurance (coverage and unit caps set by individual plan).</summary>
    Commercial,

    /// <summary>Workers' Compensation (state-regulated fee schedule; often requires specific codes).</summary>
    WorkersCompensation,

    /// <summary>Self-pay / private pay (no payer policy constraints; full CPT menu available).</summary>
    SelfPay,

    /// <summary>School district or IDEA funding (not insurer-billed; billing rules are district/state-specific).</summary>
    SchoolDistrict
}
