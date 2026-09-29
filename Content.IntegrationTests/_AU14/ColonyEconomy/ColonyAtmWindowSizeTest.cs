using System.Numerics;
using Content.Client.AU14.ColonyEconomy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._AU14.ColonyEconomy;

/// <summary>
///     Opens the ATM window on a range of screen resolutions and UI scales and checks that
///     it always fits, keeps the art's aspect ratio, and leaves the whole keypad reachable.
/// </summary>
[TestFixture]
public sealed class ColonyAtmWindowSizeTest
{
    private const float AspectRatio = 679f / 899f;

    // Physical resolution and UI scale; the window works in the resulting virtual pixels.
    private static readonly (int Width, int Height, float UiScale)[] Screens =
    {
        (3840, 2160, 2f),
        (2560, 1440, 1f),
        (1920, 1080, 1f),
        (1920, 1080, 1.25f),
        (1920, 1080, 1.5f),
        (1600, 900, 1f),
        (1366, 768, 1f),
        (1366, 768, 1.25f),
        (1280, 720, 1f),
        (1280, 720, 1.5f),
        (1280, 1024, 1f),
        (1024, 768, 1f),
        (1024, 600, 1f),
        (800, 600, 1f),
    };

    // Edge-inclusive containment with a little slack for float layout rounding.
    private static bool Inside(UIBox2 outer, UIBox2 inner)
    {
        const float slack = 0.5f;
        return inner.Left >= outer.Left - slack && inner.Top >= outer.Top - slack &&
               inner.Right <= outer.Right + slack && inner.Bottom <= outer.Bottom + slack;
    }

    [Test]
    public async Task AtmWindowFitsEveryScreen()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        foreach (var (width, height, uiScale) in Screens)
        {
            var screenSize = new Vector2(width, height) / uiScale;
            LayoutContainer screen = default!;
            ColonyAtmWindow window = default!;

            await client.WaitPost(() =>
            {
                // Stand-in for the game window at this resolution.
                screen = new LayoutContainer { SetSize = screenSize };
                uiMan.WindowRoot.AddChild(screen);
                LayoutContainer.SetPosition(screen, Vector2.Zero);

                window = new ColonyAtmWindow();
                screen.AddChild(window);
                LayoutContainer.SetPosition(window, Vector2.Zero);
            });

            await client.WaitRunTicks(10);

            await client.WaitAssertion(() =>
            {
                var name = $"{width}x{height} @ {uiScale}x";
                var screenBox = UIBox2.FromDimensions(Vector2.Zero, screenSize);
                var windowBox = UIBox2.FromDimensions(window.Position, window.Size);
                TestContext.Out.WriteLine($"{name}: screen {screenSize.X:0}x{screenSize.Y:0}, ATM {window.Size.X:0}x{window.Size.Y:0}");

                Assert.Multiple(() =>
                {
                    Assert.That(window.Size.X, Is.LessThanOrEqualTo(screenSize.X + 0.5f), $"{name}: window wider than screen");
                    Assert.That(window.Size.Y, Is.LessThanOrEqualTo(screenSize.Y + 0.5f), $"{name}: window taller than screen");
                    Assert.That(window.Size.X / window.Size.Y, Is.EqualTo(AspectRatio).Within(0.01f), $"{name}: art is stretched");
                    Assert.That(Inside(screenBox, windowBox), $"{name}: window {windowBox} is off screen {screenBox}");

                    // Every key, ENTER included, sits inside the window and on screen.
                    foreach (var key in new[] { window.Btn1, window.Btn9, window.BtnDel, window.Btn0, window.BtnOk, window.BtnEnter })
                    {
                        var keyBox = UIBox2.FromDimensions(key.GlobalPosition - screen.GlobalPosition, key.Size);
                        Assert.That(key.Size.X, Is.GreaterThan(0), $"{name}: key has no size");
                        Assert.That(Inside(windowBox, keyBox), $"{name}: key {keyBox} outside window {windowBox}");
                    }
                });

                screen.Orphan();
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtmWindowOpensCentered()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        ColonyAtmWindow window = default!;
        await client.WaitPost(() =>
        {
            window = new ColonyAtmWindow();
            window.OpenCentered();
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            var root = uiMan.WindowRoot.Size;
            var center = window.Position + window.Size / 2;
            Assert.That(center.X, Is.EqualTo(root.X / 2).Within(2f), $"Window at {window.Position} is not centred on {root}");
            Assert.That(center.Y, Is.EqualTo(root.Y / 2).Within(2f), $"Window at {window.Position} is not centred on {root}");
            window.Close();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtmWindowFollowsScreenResize()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        LayoutContainer screen = default!;
        ColonyAtmWindow window = default!;
        var bigSize = Vector2.Zero;

        await client.WaitPost(() =>
        {
            screen = new LayoutContainer { SetSize = new Vector2(1920, 1080) };
            uiMan.WindowRoot.AddChild(screen);
            LayoutContainer.SetPosition(screen, Vector2.Zero);

            window = new ColonyAtmWindow();
            screen.AddChild(window);
            // Parked in the bottom-right corner, where a shrinking screen would push it off.
            LayoutContainer.SetPosition(window, new Vector2(1300, 300));
        });
        await client.WaitRunTicks(10);

        await client.WaitPost(() =>
        {
            bigSize = window.Size;
            screen.SetSize = new Vector2(1024, 600);
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            var screenBox = UIBox2.FromDimensions(Vector2.Zero, screen.SetSize);
            Assert.Multiple(() =>
            {
                Assert.That(window.Size.Y, Is.LessThan(bigSize.Y), "Window did not shrink with the screen");
                Assert.That(Inside(screenBox, UIBox2.FromDimensions(window.Position, window.Size)), "Window left off screen after resize");
            });

            screen.SetSize = new Vector2(1920, 1080);
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(window.Size.X, Is.EqualTo(bigSize.X).Within(0.5f), "Window did not grow back to its normal size");
            screen.Orphan();
        });

        await pair.CleanReturnAsync();
    }
}
