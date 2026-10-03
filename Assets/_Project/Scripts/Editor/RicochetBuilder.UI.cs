using Ricochet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.EditorTools
{
    /// <summary>
    /// Builds the whole UI in code: one Screen Space Overlay canvas (1080x1920 reference, match 0.5),
    /// a SafeArea container, and one child per panel. Neon sci-fi style: dark translucent cards, cyan/magenta accents.
    /// </summary>
    public static partial class RicochetBuilder
    {
        static readonly Color PanelBg = new Color(0.03f, 0.05f, 0.1f, 0.88f);
        static readonly Color Dim = new Color(0f, 0.01f, 0.03f, 0.55f);
        static readonly Color ButtonBg = new Color(0.08f, 0.12f, 0.2f, 0.95f);
        static readonly Color SubText = new Color(0.7f, 0.8f, 0.9f);

        static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        static readonly Vector2 TL = new Vector2(0f, 1f);
        static readonly Vector2 TC = new Vector2(0.5f, 1f);
        static readonly Vector2 TR = new Vector2(1f, 1f);
        static readonly Vector2 BL = new Vector2(0f, 0f);
        static readonly Vector2 BR = new Vector2(1f, 0f);

        // ---------------- helpers ----------------

        static RectTransform UIRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform StretchRect(string name, Transform parent)
        {
            var rt = UIRect(name, parent, C, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color,
            Vector2 anchor, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var rt = UIRect(name, parent, anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        static Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
            Color bg, Color textColor, float fontSize, out TextMeshProUGUI labelText, Color? outline = null)
        {
            var rt = UIRect(name, parent, anchor, pos, size);
            var img = Img(rt, Spr("UI_Rounded"), bg, true);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.selectedColor = Color.white;
            b.colors = colors;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;

            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = outline ?? new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f);
            o.effectDistance = new Vector2(3f, -3f);

            labelText = Txt(rt, "Label", label, fontSize, textColor, C, Vector2.zero, size);
            return b;
        }

        static Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color textColor, float fontSize)
            => Btn(parent, name, label, anchor, pos, size, bg, textColor, fontSize, out _);

        static T MakePanel<T>(Transform parent, string name, bool dimBackground) where T : UIPanel
        {
            var rt = StretchRect(name, parent);
            rt.gameObject.AddComponent<CanvasGroup>();
            if (dimBackground)
            {
                var bg = StretchRect("Dim", rt);
                // Extend beyond the safe area so the whole screen is dimmed.
                bg.offsetMin = new Vector2(-300f, -300f);
                bg.offsetMax = new Vector2(300f, 300f);
                Img(bg, null, Dim, true);
            }
            return rt.gameObject.AddComponent<T>();
        }

        static RectTransform Card(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var rt = UIRect(name, parent, C, pos, size);
            Img(rt, Spr("UI_Rounded"), PanelBg);
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(Magenta.r, Magenta.g, Magenta.b, 0.5f);
            o.effectDistance = new Vector2(2f, -2f);
            return rt;
        }

        // ---------------- build ----------------

        static void BuildUI()
        {
            var canvasGo = new GameObject("UI Canvas", typeof(RectTransform));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var safe = StretchRect("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeArea>();

            // Floating score text sits under every panel.
            var floatRoot = StretchRect("FloatingText", safe);
            var floatPool = floatRoot.gameObject.AddComponent<FloatingTextPool>();
            Set(floatPool, ("prefab", LoadPrefab("P_FloatingText").GetComponent<FloatingText>()));

            var hud = BuildHUD(safe);
            var mainMenu = BuildMainMenu(safe);
            var leaderboard = BuildLeaderboard(safe);
            var scan = BuildScan(safe);
            var countdown = BuildCountdown(safe);
            var pause = BuildPause(safe);
            var end = BuildEnd(safe);

            var ui = canvasGo.AddComponent<UIManager>();
            Set(ui, ("mainMenu", mainMenu), ("leaderboard", leaderboard), ("scan", scan), ("countdown", countdown),
                ("hud", hud), ("pause", pause), ("end", end), ("floatingText", floatPool));
        }

        static MainMenuPanel BuildMainMenu(Transform parent)
        {
            var p = MakePanel<MainMenuPanel>(parent, "MainMenuPanel", true);
            var t = p.transform;

            Txt(t, "Title", "RICOCHET", 160, Cyan, C, new Vector2(0, 640), new Vector2(1000, 200));
            var bar = UIRect("TitleBar", t, C, new Vector2(0, 545), new Vector2(520, 8));
            Img(bar, null, Magenta);
            Txt(t, "Subtitle", "AR LASER SURVIVAL", 44, Magenta, C, new Vector2(0, 490), new Vector2(900, 70));
            var best = Txt(t, "Best", "", 38, SubText, C, new Vector2(0, 420), new Vector2(800, 60));

            var start = Btn(t, "StartButton", "START", C, new Vector2(0, 240), new Vector2(760, 180), Cyan, new Color(0.02f, 0.05f, 0.1f), 72);
            var lb = Btn(t, "LeaderboardButton", "LEADERBOARD", C, new Vector2(0, 40), new Vector2(760, 150), ButtonBg, Color.white, 54);

            Txt(t, "DifficultyLabel", "DIFFICULTY", 36, SubText, C, new Vector2(0, -110), new Vector2(800, 60));
            var easy = Btn(t, "EasyButton", "EASY", C, new Vector2(-260, -220), new Vector2(240, 140), ButtonBg, Color.white, 44);
            var normal = Btn(t, "NormalButton", "NORMAL", C, new Vector2(0, -220), new Vector2(240, 140), ButtonBg, Color.white, 44);
            var hard = Btn(t, "HardButton", "HARD", C, new Vector2(260, -220), new Vector2(240, 140), ButtonBg, Color.white, 44);
            var info = Txt(t, "DifficultyInfo", "", 32, SubText, C, new Vector2(0, -330), new Vector2(950, 60), TextAlignmentOptions.Center, false);

            var vib = Btn(t, "VibrationButton", "VIBRATION: ON", C, new Vector2(0, -480), new Vector2(560, 120), ButtonBg, Color.white, 38, out var vibLabel);
            var quit = Btn(t, "QuitButton", "QUIT", C, new Vector2(0, -630), new Vector2(400, 120), ButtonBg, new Color(1f, 0.5f, 0.6f), 40,
                out _, new Color(1f, 0.3f, 0.5f, 0.6f));

            Txt(t, "Credit", FullName, 28, new Color(1f, 1f, 1f, 0.5f), C, new Vector2(0, -820), new Vector2(900, 50), TextAlignmentOptions.Center, false);

            Set(p, ("startButton", start), ("leaderboardButton", lb), ("quitButton", quit), ("vibrationButton", vib), ("vibrationLabel", vibLabel),
                ("difficultyButtons", new Object[] { easy, normal, hard }), ("difficultyInfo", info), ("bestScoreText", best),
                ("selectedColor", Magenta), ("unselectedColor", ButtonBg));
            return p;
        }

        static LeaderboardPanel BuildLeaderboard(Transform parent)
        {
            var p = MakePanel<LeaderboardPanel>(parent, "LeaderboardPanel", true);
            var t = p.transform;
            Txt(t, "Title", "LEADERBOARD", 96, Cyan, C, new Vector2(0, 720), new Vector2(1000, 130));
            Txt(t, "Subtitle", "Your latest 5 runs", 36, Magenta, C, new Vector2(0, 630), new Vector2(900, 60), TextAlignmentOptions.Center, false);

            var rows = new Object[5];
            for (int i = 0; i < 5; i++)
            {
                var r = Card(t, $"Row{i + 1}", new Vector2(0, 440 - i * 200), new Vector2(960, 180));
                var row = r.gameObject.AddComponent<LeaderboardRow>();
                var rank = Txt(r, "Rank", $"#{i + 1}", 64, Cyan, C, new Vector2(-385, 0), new Vector2(150, 120));
                var score = Txt(r, "Score", "0", 58, Color.white, C, new Vector2(70, 30), new Vector2(760, 80), TextAlignmentOptions.Left);
                var detail = Txt(r, "Detail", "", 30, SubText, C, new Vector2(70, -40), new Vector2(760, 60), TextAlignmentOptions.Left, false);
                Set(row, ("rankText", rank), ("scoreText", score), ("detailText", detail), ("background", r.GetComponent<Image>()));
                rows[i] = row;
            }
            var empty = Txt(t, "EmptyText", "No runs yet.\nPlay a round to set a score!", 48, SubText, C, new Vector2(0, 100), new Vector2(900, 200));

            var clear = Btn(t, "ClearButton", "CLEAR", C, new Vector2(-230, -650), new Vector2(420, 140), ButtonBg, new Color(1f, 0.5f, 0.6f), 44,
                out var clearLabel, new Color(1f, 0.3f, 0.5f, 0.6f));
            var back = Btn(t, "BackButton", "BACK", C, new Vector2(230, -650), new Vector2(420, 140), Cyan, new Color(0.02f, 0.05f, 0.1f), 50);

            Set(p, ("rows", rows), ("emptyText", empty), ("clearButton", clear), ("clearLabel", clearLabel), ("backButton", back));
            return p;
        }

        static ScanPanel BuildScan(Transform parent)
        {
            // No full-screen background here: taps must reach the AR view to place the beacon.
            var p = MakePanel<ScanPanel>(parent, "ScanPanel", false);
            var t = p.transform;
            var card = Card(t, "InstructionCard", new Vector2(0, 640), new Vector2(940, 280));
            var instruction = Txt(card, "Instruction", "", 52, Color.white, C, Vector2.zero, new Vector2(880, 240));

            var phone = UIRect("PhoneIcon", t, C, new Vector2(0, 340), new Vector2(130, 220));
            Img(phone, Spr("UI_Rounded"), new Color(0.2f, 1f, 1f, 0.25f));
            var po = phone.gameObject.AddComponent<Outline>();
            po.effectColor = Cyan;
            po.effectDistance = new Vector2(4f, -4f);
            var screen = UIRect("Screen", phone, C, new Vector2(0, 5), new Vector2(100, 160));
            Img(screen, Spr("UI_Rounded"), new Color(0.2f, 1f, 1f, 0.4f));

            Txt(t, "Hint", "Tip: textured, well-lit floors scan fastest.\nWalls are detected too - lasers bounce off them!", 32, SubText,
                C, new Vector2(0, -560), new Vector2(950, 120), TextAlignmentOptions.Center, false);
            var back = Btn(t, "BackButton", "BACK", C, new Vector2(0, -740), new Vector2(400, 130), ButtonBg, Color.white, 46);

            Set(p, ("instruction", instruction), ("phoneIcon", phone), ("backButton", back));
            return p;
        }

        static CountdownPanel BuildCountdown(Transform parent)
        {
            var p = MakePanel<CountdownPanel>(parent, "CountdownPanel", false);
            var number = Txt(p.transform, "Number", "3", 400, Cyan, C, new Vector2(0, 100), new Vector2(900, 500));
            Txt(p.transform, "Hint", "Hold FIRE to shoot. Bounce lasers off walls for bonus points!", 36, Color.white,
                C, new Vector2(0, -250), new Vector2(950, 120), TextAlignmentOptions.Center, false);
            Set(p, ("numberText", number));
            return p;
        }

        static HUDPanel BuildHUD(Transform parent)
        {
            var p = MakePanel<HUDPanel>(parent, "HUDPanel", false);
            var t = p.transform;

            // Full-screen feedback overlays (never block touches).
            var vig = StretchRect("DamageVignette", t);
            vig.offsetMin = new Vector2(-300f, -300f);
            vig.offsetMax = new Vector2(300f, 300f);
            Img(vig, Spr("UI_Vignette"), new Color(1f, 0.05f, 0.1f, 1f));
            var vigGroup = vig.gameObject.AddComponent<CanvasGroup>();
            vigGroup.alpha = 0f;
            vigGroup.blocksRaycasts = false;
            vigGroup.interactable = false;

            var death = StretchRect("DeathOverlay", t);
            death.offsetMin = new Vector2(-300f, -300f);
            death.offsetMax = new Vector2(300f, 300f);
            Img(death, null, new Color(0.6f, 0f, 0.05f, 1f));
            var deathGroup = death.gameObject.AddComponent<CanvasGroup>();
            deathGroup.alpha = 0f;
            deathGroup.blocksRaycasts = false;
            deathGroup.interactable = false;

            // Health (top-left)
            var hp = UIRect("Health", t, TL, new Vector2(30, -30), new Vector2(440, 120));
            Img(hp, Spr("UI_Rounded"), PanelBg);
            Txt(hp, "Label", "HP", 34, Magenta, TL, new Vector2(20, -12), new Vector2(80, 50), TextAlignmentOptions.Left);
            var hpText = Txt(hp, "Value", "100", 44, Color.white, TR, new Vector2(-20, -8), new Vector2(150, 56), TextAlignmentOptions.Right);
            var barBg = UIRect("BarBg", hp, BL, new Vector2(20, 18), new Vector2(400, 34));
            Img(barBg, Spr("UI_Rounded"), new Color(1f, 1f, 1f, 0.12f));
            var barFill = UIRect("BarFill", barBg, C, Vector2.zero, Vector2.zero);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            var hpFill = Img(barFill, Spr("UI_Rounded"), new Color(0.2f, 1f, 0.6f));
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;

            // Score (top-centre)
            var sc = UIRect("Score", t, TC, new Vector2(0, -30), new Vector2(300, 120));
            Img(sc, Spr("UI_Rounded"), PanelBg);
            Txt(sc, "Label", "SCORE", 26, SubText, TC, new Vector2(0, -8), new Vector2(280, 36));
            var scoreText = Txt(sc, "Value", "0", 56, Cyan, BL, new Vector2(0, 6), new Vector2(300, 76));
            scoreText.rectTransform.anchorMin = scoreText.rectTransform.anchorMax = scoreText.rectTransform.pivot = new Vector2(0.5f, 0f);

            // Time (top-right)
            var tm = UIRect("Time", t, TR, new Vector2(-30, -30), new Vector2(260, 120));
            Img(tm, Spr("UI_Rounded"), PanelBg);
            Txt(tm, "Label", "TIME", 26, SubText, TC, new Vector2(0, -8), new Vector2(240, 36));
            var timeText = Txt(tm, "Value", "2:30", 56, Color.white, BL, new Vector2(0, 6), new Vector2(260, 76));
            timeText.rectTransform.anchorMin = timeText.rectTransform.anchorMax = timeText.rectTransform.pivot = new Vector2(0.5f, 0f);

            // Second row: walls hint, spread tier, pause
            var walls = Txt(t, "Walls", "WALLS: 0", 30, SubText, TL, new Vector2(40, -165), new Vector2(360, 50), TextAlignmentOptions.Left);
            var spread = Txt(t, "Spread", "SINGLE", 34, Magenta, TC, new Vector2(0, -165), new Vector2(400, 50));
            var pause = Btn(t, "PauseButton", "II", TR, new Vector2(-30, -165), new Vector2(130, 130), ButtonBg, Color.white, 56);

            // Toast (card collected)
            var toast = Txt(t, "Toast", "", 76, Color.white, C, new Vector2(0, 360), new Vector2(1000, 120));

            // Crosshair
            var cross = UIRect("Crosshair", t, C, Vector2.zero, new Vector2(84, 84));
            Img(cross, Spr("UI_Ring"), new Color(0.2f, 1f, 1f, 0.9f));
            var dot = UIRect("Dot", cross, C, Vector2.zero, new Vector2(12, 12));
            Img(dot, Spr("UI_Circle"), Magenta);

            // Fire button (bottom-right)
            var fire = UIRect("FireButton", t, BR, new Vector2(-50, 60), new Vector2(300, 300));
            Img(fire, Spr("UI_Circle"), new Color(0f, 0f, 0f, 0.001f), true); // invisible hit area
            var fireVisual = UIRect("Visual", fire, C, Vector2.zero, new Vector2(300, 300));
            Img(fireVisual, Spr("UI_Circle"), new Color(1f, 0.2f, 0.8f, 0.75f));
            var ring = UIRect("Ring", fireVisual, C, Vector2.zero, new Vector2(300, 300));
            Img(ring, Spr("UI_Ring"), Cyan);
            Txt(fireVisual, "Label", "FIRE", 64, Color.white, C, Vector2.zero, new Vector2(280, 100));
            var fireButton = fire.gameObject.AddComponent<FireButton>();
            Set(fireButton, ("visual", fireVisual));

            // Heat bar (above fire button)
            var heatLabel = Txt(t, "HeatLabel", "HEAT", 30, Cyan, BR, new Vector2(-50, 420), new Vector2(300, 44));
            var heatBg = UIRect("HeatBar", t, BR, new Vector2(-50, 380), new Vector2(300, 34));
            Img(heatBg, Spr("UI_Rounded"), new Color(1f, 1f, 1f, 0.12f));
            var heatFillRt = UIRect("Fill", heatBg, C, Vector2.zero, Vector2.zero);
            heatFillRt.anchorMin = Vector2.zero;
            heatFillRt.anchorMax = Vector2.one;
            heatFillRt.offsetMin = heatFillRt.offsetMax = Vector2.zero;
            var heatFill = Img(heatFillRt, Spr("UI_Rounded"), Cyan);
            heatFill.type = Image.Type.Filled;
            heatFill.fillMethod = Image.FillMethod.Horizontal;
            heatFill.fillAmount = 0f;

            // Gadget buttons (bottom-left; hidden until a card gives a charge)
            var mirror = GadgetButton(t, "MirrorButton", "MIRROR", new Vector2(50, 60), new Color(0.3f, 0.9f, 1f), out var mirrorCount);
            var prism = GadgetButton(t, "PrismButton", "PRISM", new Vector2(50, 290), new Color(1f, 0.3f, 0.9f), out var prismCount);

            Set(p, ("healthFill", hpFill), ("healthText", hpText), ("scoreText", scoreText), ("timeText", timeText), ("pauseButton", pause),
                ("fireButton", fireButton), ("heatFill", heatFill), ("heatLabel", heatLabel), ("spreadText", spread),
                ("mirrorButton", mirror), ("mirrorCount", mirrorCount), ("prismButton", prism), ("prismCount", prismCount),
                ("wallsText", walls), ("toastText", toast), ("damageVignette", vigGroup), ("deathOverlay", deathGroup));
            return p;
        }

        static Button GadgetButton(Transform parent, string name, string label, Vector2 pos, Color color, out TextMeshProUGUI count)
        {
            var b = Btn(parent, name, label, BL, pos, new Vector2(210, 200), new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f),
                color, 38, out var text, new Color(color.r, color.g, color.b, 0.8f));
            text.rectTransform.anchoredPosition = new Vector2(0, -40);
            var badge = UIRect("Badge", b.transform, C, new Vector2(0, 30), new Vector2(80, 80));
            Img(badge, Spr("UI_Circle"), color);
            count = Txt(badge, "Count", "0", 46, new Color(0.02f, 0.05f, 0.1f), C, Vector2.zero, new Vector2(80, 80));
            b.gameObject.SetActive(false);
            return b;
        }

        static PausePanel BuildPause(Transform parent)
        {
            var p = MakePanel<PausePanel>(parent, "PausePanel", true);
            var t = p.transform;
            var card = Card(t, "Card", new Vector2(0, 80), new Vector2(860, 1300));
            Txt(card, "Title", "PAUSED", 110, Cyan, C, new Vector2(0, 520), new Vector2(800, 140));
            var resume = Btn(card, "ResumeButton", "RESUME", C, new Vector2(0, 310), new Vector2(680, 160), Cyan, new Color(0.02f, 0.05f, 0.1f), 60);
            var restart = Btn(card, "RestartButton", "RESTART", C, new Vector2(0, 120), new Vector2(680, 150), ButtonBg, Color.white, 52);
            var menu = Btn(card, "MenuButton", "MAIN MENU", C, new Vector2(0, -60), new Vector2(680, 150), ButtonBg, Color.white, 52);
            var stats = Txt(card, "PoolStats", "", 26, SubText, C, new Vector2(0, -400), new Vector2(760, 380), TextAlignmentOptions.TopLeft, false);
            Set(p, ("resumeButton", resume), ("restartButton", restart), ("menuButton", menu), ("poolStatsText", stats));
            return p;
        }

        static EndPanel BuildEnd(Transform parent)
        {
            var p = MakePanel<EndPanel>(parent, "EndPanel", true);
            var t = p.transform;
            var card = Card(t, "Card", new Vector2(0, 40), new Vector2(940, 1400));
            var title = Txt(card, "Title", "SURVIVED", 120, Cyan, C, new Vector2(0, 560), new Vector2(900, 160));
            Txt(card, "ScoreLabel", "FINAL SCORE", 36, SubText, C, new Vector2(0, 420), new Vector2(800, 60));
            var score = Txt(card, "Score", "0", 150, Color.white, C, new Vector2(0, 300), new Vector2(900, 180));

            var tag = UIRect("NewBest", card, C, new Vector2(0, 180), new Vector2(380, 84));
            Img(tag, Spr("UI_Rounded"), Magenta);
            Txt(tag, "Label", "NEW BEST!", 46, Color.white, C, Vector2.zero, new Vector2(380, 84));

            var kills = Txt(card, "Kills", "", 50, Color.white, C, new Vector2(0, 60), new Vector2(900, 70));
            var breakdown = Txt(card, "Breakdown", "", 36, SubText, C, new Vector2(0, -10), new Vector2(900, 60), TextAlignmentOptions.Center, false);
            var time = Txt(card, "Time", "", 42, Cyan, C, new Vector2(0, -100), new Vector2(900, 60));

            var restart = Btn(card, "RestartButton", "RESTART", C, new Vector2(0, -320), new Vector2(720, 160), Cyan, new Color(0.02f, 0.05f, 0.1f), 60);
            var menu = Btn(card, "MenuButton", "MAIN MENU", C, new Vector2(0, -510), new Vector2(720, 150), ButtonBg, Color.white, 52);

            Set(p, ("titleText", title), ("scoreText", score), ("killsText", kills), ("breakdownText", breakdown), ("timeText", time),
                ("newBestTag", tag.gameObject), ("restartButton", restart), ("menuButton", menu));
            return p;
        }
    }
}
