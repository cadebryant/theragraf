using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;

namespace Theragraf.Tests.Activities;

/// <summary>
/// Verifies that <see cref="BillingActivityInput"/> correctly carries ClinicalSetting
/// and PayerType through the billing pipeline.
/// </summary>
public class BillingContextTests
{
    private static readonly SoapNote Note = new("S", "O", "A", "P");

    [Fact]
    public void BillingActivityInput_DefaultSettingAndPayer_AreOutpatientAndMedicare()
    {
        var input = new BillingActivityInput(Note, TherapyDiscipline.OccupationalTherapy, 45);

        input.Setting.Should().Be(ClinicalSetting.Outpatient);
        input.Payer.Should().Be(PayerType.Medicare);
    }

    [Theory]
    [InlineData(ClinicalSetting.Outpatient,            PayerType.Medicare)]
    [InlineData(ClinicalSetting.SkilledNursingFacility, PayerType.Medicare)]
    [InlineData(ClinicalSetting.SchoolBased,            PayerType.SchoolDistrict)]
    [InlineData(ClinicalSetting.EarlyIntervention,      PayerType.Medicaid)]
    [InlineData(ClinicalSetting.Telehealth,             PayerType.Commercial)]
    [InlineData(ClinicalSetting.HomeHealth,             PayerType.Medicare)]
    [InlineData(ClinicalSetting.Inpatient,              PayerType.Commercial)]
    public void BillingActivityInput_SettingAndPayer_RoundTripCorrectly(
        ClinicalSetting setting, PayerType payer)
    {
        var input = new BillingActivityInput(Note, TherapyDiscipline.OccupationalTherapy, 45, setting, payer);

        input.Setting.Should().Be(setting);
        input.Payer.Should().Be(payer);
    }

    [Fact]
    public void TranscriptInput_DefaultSettingAndPayer_AreOutpatientAndMedicare()
    {
        var transcript = new TranscriptInput(
            "Raw transcript.", "Dr. Adams", "client-001",
            DateTimeOffset.UtcNow, TherapyDiscipline.OccupationalTherapy, 45);

        transcript.Setting.Should().Be(ClinicalSetting.Outpatient);
        transcript.Payer.Should().Be(PayerType.Medicare);
    }

    [Fact]
    public void TranscriptInput_ExplicitSnfMedicare_Persists()
    {
        var transcript = new TranscriptInput(
            "Raw transcript.", "Dr. Adams", "client-001",
            DateTimeOffset.UtcNow, TherapyDiscipline.OccupationalTherapy, 45,
            ClinicalSetting.SkilledNursingFacility, PayerType.Medicare);

        transcript.Setting.Should().Be(ClinicalSetting.SkilledNursingFacility);
        transcript.Payer.Should().Be(PayerType.Medicare);
    }

    [Fact]
    public void TranscriptInput_SchoolBasedDistrict_Persists()
    {
        var transcript = new TranscriptInput(
            "Raw transcript.", "Dr. Adams", "client-001",
            DateTimeOffset.UtcNow, TherapyDiscipline.OccupationalTherapy, null,
            ClinicalSetting.SchoolBased, PayerType.SchoolDistrict);

        transcript.Setting.Should().Be(ClinicalSetting.SchoolBased);
        transcript.Payer.Should().Be(PayerType.SchoolDistrict);
    }
}
