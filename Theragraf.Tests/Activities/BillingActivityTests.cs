using FluentAssertions;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;
using Theragraf.Functions.Agents;

namespace Theragraf.Tests.Activities;

public class BillingActivityTests
{
    private readonly IBillingAgent _billingAgent;
    private readonly BillingActivity _sut;

    private static readonly SoapNote Note = new("S", "O", "A", "P");
    private static readonly CptCode Code1 = new("97530", "Therapeutic activities", "Reason A");
    private static readonly CptCode Code2 = new("97110", "Therapeutic exercises", "Reason B");

    public BillingActivityTests()
    {
        _billingAgent = Substitute.For<IBillingAgent>();
        _sut = new BillingActivity(_billingAgent);
    }

    [Fact]
    public async Task Run_DelegatesToBillingAgent()
    {
        var input = new BillingActivityInput(Note, TherapyDiscipline.OccupationalTherapy, 45);
        _billingAgent.SuggestCptCodesAsync(Note, TherapyDiscipline.OccupationalTherapy, 45, ClinicalSetting.Outpatient, PayerType.Medicare)
            .Returns(new List<CptCode> { Code1 });

        var result = await _sut.Run(input);

        result.Should().ContainSingle().Which.Should().Be(Code1);
    }

    [Fact]
    public async Task Run_PassesCorrectDisciplineAndDuration()
    {
        var input = new BillingActivityInput(Note, TherapyDiscipline.PhysicalTherapy, 60);
        _billingAgent.SuggestCptCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<int?>(), Arg.Any<ClinicalSetting>(), Arg.Any<PayerType>())
            .Returns(new List<CptCode>());

        await _sut.Run(input);

        await _billingAgent.Received(1).SuggestCptCodesAsync(Note, TherapyDiscipline.PhysicalTherapy, 60, ClinicalSetting.Outpatient, PayerType.Medicare);
    }

    [Fact]
    public async Task Run_NullSessionDuration_PassedThroughToAgent()
    {
        var input = new BillingActivityInput(Note, TherapyDiscipline.OccupationalTherapy, null);
        _billingAgent.SuggestCptCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), null, Arg.Any<ClinicalSetting>(), Arg.Any<PayerType>())
            .Returns(new List<CptCode> { Code1 });

        var result = await _sut.Run(input);

        await _billingAgent.Received(1).SuggestCptCodesAsync(Note, TherapyDiscipline.OccupationalTherapy, null, ClinicalSetting.Outpatient, PayerType.Medicare);
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Run_ReturnsAllCodesFromAgent()
    {
        var input = new BillingActivityInput(Note, TherapyDiscipline.OccupationalTherapy, 45);
        _billingAgent.SuggestCptCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<int?>(), Arg.Any<ClinicalSetting>(), Arg.Any<PayerType>())
            .Returns(new List<CptCode> { Code1, Code2 });

        var result = await _sut.Run(input);

        result.Should().HaveCount(2).And.Contain(Code1).And.Contain(Code2);
    }

    [Fact]
    public async Task Run_AgentThrows_ExceptionPropagates()
    {
        var input = new BillingActivityInput(Note, TherapyDiscipline.OccupationalTherapy, 45);
        _billingAgent.SuggestCptCodesAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<int?>(), Arg.Any<ClinicalSetting>(), Arg.Any<PayerType>())
            .Returns<IReadOnlyList<CptCode>>(_ => throw new InvalidOperationException("LLM unavailable"));

        var act = async () => await _sut.Run(input);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("LLM unavailable");
    }
}
