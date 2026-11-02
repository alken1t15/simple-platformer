using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class SimulationSettingsTests
{
    [TestMethod]
    public void DefaultValuesMatchCoreContract()
    {
        var s = SimulationSettings.Default;
        Assert.AreEqual(1f / 120f, s.FixedStepSeconds);
        Assert.AreEqual(0.5f, s.BallRadius);
        Assert.AreEqual(4.5f, s.MoveSpeed);
        Assert.AreEqual(20f, s.MoveAcceleration);
        Assert.AreEqual(24f, s.BrakingAcceleration);
        Assert.AreEqual(0.25f, s.AirControlFactor);
        Assert.AreEqual(18f, s.Gravity);
        Assert.AreEqual(6f, s.JumpSpeed);
        Assert.AreEqual(1.8f, s.BoostMultiplier);
        Assert.AreEqual(1f, s.BoostDurationSeconds);
        Assert.AreEqual(3f, s.BoostCooldownSeconds);
        Assert.AreEqual(50f * MathF.PI / 180f, s.MaxSlopeRadians);
        Assert.AreEqual(0.001f, s.SkinWidth);
        Assert.AreEqual(1e-5f, s.PositionEpsilon);
        Assert.AreEqual(1e-4f, s.PenetrationSlop);
        Assert.AreEqual(32, s.MaxCollisionSubsteps);
        Assert.AreEqual(8, s.MaxContactIterations);
        Assert.AreEqual(30f, s.MaxFallSpeed);
        Assert.IsTrue(typeof(SimulationSettings).GetProperties().All(p => p.SetMethod is null));
    }

    [TestMethod]
    public void InvalidFieldsAndCombinationsReportParameter()
    {
        (string Name, Action Create)[] invalid =
        [
            ("fixedStepSeconds", () => new SimulationSettings(fixedStepSeconds: 1f / 300f)),
            ("ballRadius", () => new SimulationSettings(ballRadius: 0.09f)),
            ("moveSpeed", () => new SimulationSettings(moveSpeed: 0f)),
            ("moveAcceleration", () => new SimulationSettings(moveAcceleration: 101f)),
            ("brakingAcceleration", () => new SimulationSettings(brakingAcceleration: float.NaN)),
            ("airControlFactor", () => new SimulationSettings(airControlFactor: 1.01f)),
            ("gravity", () => new SimulationSettings(gravity: float.PositiveInfinity)),
            ("jumpSpeed", () => new SimulationSettings(jumpSpeed: -1f)),
            ("boostMultiplier", () => new SimulationSettings(boostMultiplier: 3.01f)),
            ("boostDurationSeconds", () => new SimulationSettings(boostDurationSeconds: 0f)),
            ("boostCooldownSeconds", () => new SimulationSettings(boostCooldownSeconds: -0.1f)),
            ("maxSlopeRadians", () => new SimulationSettings(maxSlopeRadians: 0f)),
            ("skinWidth", () => new SimulationSettings(skinWidth: 0.01f, ballRadius: 0.1f)),
            ("positionEpsilon", () => new SimulationSettings(skinWidth: 0.0001f, positionEpsilon: 0.0001f)),
            ("penetrationSlop", () => new SimulationSettings(penetrationSlop: 1e-6f)),
            ("maxCollisionSubsteps", () => new SimulationSettings(maxCollisionSubsteps: 1)),
            ("maxCollisionSubsteps", () => new SimulationSettings(maxCollisionSubsteps: 129)),
            ("maxContactIterations", () => new SimulationSettings(maxContactIterations: 0)),
            ("maxFallSpeed", () => new SimulationSettings(maxFallSpeed: 5f)),
        ];
        foreach (var (name, create) in invalid)
            Assert.AreEqual(name, Assert.ThrowsExactly<ArgumentOutOfRangeException>(create).ParamName);
    }

    [TestMethod]
    public void FastSmallBallRequiresAdequateBudgetBeforeSimulation()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SimulationSettings(
            fixedStepSeconds: 1f / 30f, ballRadius: 0.1f, moveSpeed: 20f, boostMultiplier: 3f,
            maxCollisionSubsteps: 32));
        var accepted = new SimulationSettings(fixedStepSeconds: 1f / 30f, ballRadius: 0.1f,
            moveSpeed: 20f, boostMultiplier: 3f, maxCollisionSubsteps: 128);
        Assert.AreEqual(128, accepted.MaxCollisionSubsteps);
    }
}
