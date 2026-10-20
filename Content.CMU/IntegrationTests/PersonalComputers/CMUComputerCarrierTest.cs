using Content.IntegrationTests.Fixtures;
using Content.Server.Power.Components;
using Content.Shared._RMC14.Power;
using Content.Shared.CMU14.PersonalComputers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.PersonalComputers;

/// <summary>Terminals need the colony sensor tower for the network, and the area's equipment power to run.</summary>
[TestFixture]
public sealed class CMUComputerCarrierTest : GameTest
{
    [Test]
    [Description("A broken sensor tower cuts the network, a working one brings it back, and a colony with no tower keeps it.")]
    public async Task SensorTowerCarriesTheNetwork()
    {
        var map = await Pair.CreateTestMap();
        EntityUid computer = default;
        await Server.WaitPost(() => computer = SEntMan.SpawnEntity("RMCPersonalDesktop", map.GridCoords));
        await Pair.RunTicksSync(90);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUPersonalComputerComponent>(computer).Carrier, "A colony with no tower lost its network."));

        await Server.WaitPost(() => SEntMan.SpawnEntity("AU14SensorTower", map.GridCoords));
        await Pair.RunTicksSync(90);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUPersonalComputerComponent>(computer).Carrier, Is.False, "The network stayed up with the only tower broken."));

        await Server.WaitPost(() => SEntMan.SpawnEntity("RMCSensorTowerOn", map.GridCoords));
        await Pair.RunTicksSync(90);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUPersonalComputerComponent>(computer).Carrier, "A working tower did not bring the network back."));
    }

    [Test]
    [Description("Powered terminals carry both receivers: RMC's power check reads the apc receiver, the area power the RMC one.")]
    public void TerminalsHaveBothPowerReceivers()
    {
        var prototypes = Server.ResolveDependency<IPrototypeManager>();
        var factory = Server.ResolveDependency<IComponentFactory>();
        foreach (var id in new[] { "RMCPersonalDesktop", "CMUMonitorCameraColonyCMB" })
        {
            var proto = prototypes.Index<EntityPrototype>(id);
            Assert.That(proto.Components.ContainsKey(factory.GetComponentName<RMCPowerReceiverComponent>()), $"{id} has no RMC power receiver.");
            Assert.That(proto.Components.ContainsKey(factory.GetComponentName<ApcPowerReceiverComponent>()), $"{id} has no apc power receiver, so it never counts as powered.");
        }
    }
}
