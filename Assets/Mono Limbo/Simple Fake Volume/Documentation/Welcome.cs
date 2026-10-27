using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonoLimbo
{
    [InitializeOnLoad]
    public static class SimpleFakeVolume_WelcomeLauncher
    {
        static SimpleFakeVolume_WelcomeLauncher()
        {
            // Only auto-open once per project, not once per machine.
            if (!EditorPrefs.HasKey(SimpleFakeVolume_WelcomeWindow.DontShowKey))
                EditorApplication.update += OpenWindowOnce;
        }

        private static void OpenWindowOnce()
        {
            EditorApplication.update -= OpenWindowOnce;
            SimpleFakeVolume_WelcomeWindow.ShowWindow();
        }
    }

    public class SimpleFakeVolume_WelcomeWindow : EditorWindow
    {
        // ------------------------------------------------------------------
        //  Config
        // ------------------------------------------------------------------

        private const string CurrentVersion = "3.0.0";

        private const string AssetReviewUrl  = "https://assetstore.unity.com/packages/vfx/shaders/simple-fake-volume-fog-299560#reviews";
        private const string PublisherUrl    = "https://assetstore.unity.com/publishers/98904";
        private const string SupportEmail    = "monolimbostudio@gmail.com";

        // Local docs/banner are found automatically by name inside the project — no hosted
        // URL or Resources folder required. Falls back gracefully if a file is renamed/moved.
        private const string DocumentationSearchName = "Documentation";
        private const string DocumentationExtension  = ".pdf";
        private const string BannerSearchName        = "SimpleFake";
        private string documentationPdfPath;
        private Texture2D bannerCache;

        // EditorPrefs keys are scoped per-project (via Application.dataPath hash) so dismissing
        // the window in one project doesn't silently suppress it in every other project on the machine.
        private static readonly string ProjectKeySuffix = Application.dataPath.GetHashCode().ToString();
        public static string DontShowKey => "SimpleFakeVolume_Welcome_DontShow_" + ProjectKeySuffix;
        private static string SessionCountKey => "SimpleFakeVolume_Welcome_OpenCount_" + ProjectKeySuffix;

        private enum Tab { Home, WhatsNew, Documentation }
        private Tab currentTab = Tab.Home;

        private bool dontShowAgain;
        private Vector2 changelogScroll;
        private Vector2 docsScroll;
        private int openCount;

        // Cached styles — built once in OnEnable instead of every OnGUI call.
        private GUIStyle bodyStyle;
        private GUIStyle headerStyle;
        private GUIStyle paddedBox;
        private GUIStyle centeredBody;
        private GUIStyle versionBadge;
        private GUIStyle sectionLabel;
        private GUIStyle toolbarStyle;
        private bool stylesBuilt;

        // ------------------------------------------------------------------
        //  Changelog data (pulled from the provided release notes)
        // ------------------------------------------------------------------

        private class ChangelogEntry
        {
            public string Version;
            public string Title;
            public string Blurb;
            public bool IsCurrent;
            public bool IsUpcoming;
            public List<(string Heading, string[] Items)> Sections;
        }

        private static readonly List<ChangelogEntry> Changelog = new List<ChangelogEntry>
        {
            new ChangelogEntry
            {
                Version = "3.0.0",
                Title = "Point Light & Procedural Update",
                Blurb = "A massive expansion introducing point lights, procedural script workflows, and a completely revamped onboarding experience.",
                IsCurrent = true,
                Sections = new List<(string, string[])>
                {
                    ("Added", new[]
                    {
                        "Point Light Support: full volumetric support for point lights alongside spotlights and cones",
                        "10 ready-to-use volumetric prefabs for instant drag-and-drop setup",
                        "9 complete sample scenes, including Horror, Street, Party, and Passage setups",
                        "Procedural Script Toolkit: 5 scripts (PartyLightSwing, LightFlicker, IdleCameraDolly, HandheldCameraShake, PartyLightController)",
                        "Bonus SFX (wind loops, lightbulb buzz) and Lens Flare components"
                    }),
                    ("Improved", new[]
                    {
                        "Complete UI overhaul of the Editor Welcome Hub",
                        "Refined and finalized all text documentation for clarity"
                    })
                }
            },
            new ChangelogEntry
            {
                Version = "2.5.0",
                Title = "In Progress (Upcoming)",
                Blurb = "Next major update focuses on advanced post-processing and expanding the visual effects library.",
                IsUpcoming = true,
                Sections = new List<(string, string[])>
                {
                    ("In Development", new[]
                    {
                        "Screen-space post-processing for God Rays",
                        "Expanding the VFX library with additional God Ray sample scenes"
                    }),
                    ("Planned", new[]
                    {
                        "Further rendering performance optimizations and structural enhancements"
                    })
                }
            },
            new ChangelogEntry
            {
                Version = "2.0.0",
                Title = "Major Feature Update",
                Blurb = "A substantial overhaul expanding out-of-the-box content, improving performance, and refining usability.",
                Sections = new List<(string, string[])>
                {
                    ("Added", new[]
                    {
                        "7 new sample scenes demonstrating diverse use cases",
                        "Cross-shaped and Cylinder-shaped Fake Rays",
                        "Expanded base library with additional starting prefabs",
                        "Bonus content: Sky Shader, HandheldCameraShake, IdleCameraDolly",
                        "Extra shader control parameters for finer visual tweaking"
                    }),
                    ("Improved", new[]
                    {
                        "Significantly optimized overall shader performance",
                        "Cleaned up internal shader node architecture and hierarchy",
                        "Refined default presets for better out-of-the-box results",
                        "Streamlined the user experience"
                    }),
                    ("Fixed", new[]
                    {
                        "Resolved an issue regarding a missing default material"
                    })
                }
            },
            new ChangelogEntry
            {
                Version = "1.5.0",
                Title = "Quality of Life Update",
                Blurb = "Focused on improving the initial onboarding experience and keeping your project clean.",
                Sections = new List<(string, string[])>
                {
                    ("Added", new[]
                    {
                        "Welcome Window for faster initial setup and direct access to documentation"
                    }),
                    ("Improved", new[]
                    {
                        "Reorganized folder structure to improve project clarity",
                        "Simplified the core workflow for a smoother first-time user experience"
                    })
                }
            },
        };

        // ------------------------------------------------------------------
        //  Window lifecycle
        // ------------------------------------------------------------------

        [MenuItem("Tools/MonoLimbo/Simple Fake Volume")]
        [MenuItem("Window/MonoLimbo/Simple Fake Volume")]
        public static void ShowWindow()
        {
            // utility:true = floating, non-dockable tool window. This is what actually makes
            // minSize/maxSize get enforced — a docked window largely ignores maxSize in Unity.
            var window = GetWindow<SimpleFakeVolume_WelcomeWindow>(true, "Simple Fake Volume", true);
            window.minSize = new Vector2(480, 640);
            window.maxSize = new Vector2(480, 920);
        }

        private void OnEnable()
        {
            bannerCache = FindTextureInProject(BannerSearchName);
            documentationPdfPath = FindAssetPathByNameAndExtension(DocumentationSearchName, DocumentationExtension);

            if (bannerCache == null)
                Debug.LogWarning("[Simple Fake Volume] Banner texture not found in the project — check it wasn't excluded from the package export.");
            if (string.IsNullOrEmpty(documentationPdfPath))
                Debug.LogWarning("[Simple Fake Volume] Documentation.pdf not found in the project — check it wasn't excluded from the package export.");

            dontShowAgain = EditorPrefs.HasKey(DontShowKey);

            openCount = EditorPrefs.GetInt(SessionCountKey, 0) + 1;
            EditorPrefs.SetInt(SessionCountKey, openCount);

            stylesBuilt = false; // rebuild next OnGUI (EditorStyles isn't always safe to touch in OnEnable)
        }

        /// <summary>Finds an asset anywhere in the project by (partial) name + exact extension, without relying on a fixed path.</summary>
        private static string FindAssetPathByNameAndExtension(string nameContains, string extension)
        {
            var guids = AssetDatabase.FindAssets(nameContains);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        /// <summary>Finds the first Texture2D anywhere in the project whose name contains the given text.</summary>
        private static Texture2D FindTextureInProject(string nameContains)
        {
            var guids = AssetDatabase.FindAssets(nameContains + " t:Texture2D");
            if (guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private void BuildStyles()
        {
            bool dark = EditorGUIUtility.isProSkin;

            bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                richText = true
            };

            centeredBody = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 6, 3)
            };

            sectionLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = dark ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.15f, 0.15f, 0.15f) }
            };

            paddedBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 6)
            };

            versionBadge = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 9,
                fixedHeight = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            toolbarStyle = EditorStyles.toolbarButton;

            stylesBuilt = true;
        }

        // ------------------------------------------------------------------
        //  GUI
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!stylesBuilt) BuildStyles();

            DrawTabBar();

            GUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.BeginVertical();
            GUILayout.Space(10);

            switch (currentTab)
            {
                case Tab.Home:          DrawHomeTab(); break;
                case Tab.WhatsNew:      DrawWhatsNewTab(); break;
                case Tab.Documentation: DrawDocumentationTab(); break;
            }

            GUILayout.Space(8);
            GUILayout.EndVertical();
            GUILayout.Space(12);
            GUILayout.EndHorizontal();
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawTabButton("Home", Tab.Home);
            DrawTabButton("What's New", Tab.WhatsNew);
            DrawTabButton("Documentation", Tab.Documentation);
            GUILayout.EndHorizontal();
        }

        private void DrawTabButton(string label, Tab tab)
        {
            bool selected = currentTab == tab;
            var style = new GUIStyle(toolbarStyle) { fontStyle = selected ? FontStyle.Bold : FontStyle.Normal };
            if (GUILayout.Toggle(selected, label, style, GUILayout.MinWidth(80)))
                currentTab = tab;
        }

        /// <summary>Draws a texture inside a fixed-height strip, scaled to fit (no stretching/distortion) and clipped to the window width.</summary>
        private static void DrawBanner(Texture2D tex, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));

            // Background fill so any letterboxed edges blend with the window instead of showing as empty grey.
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.8f, 0.8f, 0.8f));
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
            }
        }

        // ---- Home tab -------------------------------------------------

        private void DrawHomeTab()
        {
            if (bannerCache != null)
            {
                DrawBanner(bannerCache, 110f);
                GUILayout.Space(6);
            }
            else
            {
                GUILayout.Label("SIMPLE FAKE VOLUME FOG", new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
                GUILayout.Space(10);
            }

            // Version label
            GUILayout.Label("v" + CurrentVersion, EditorStyles.miniLabel);
            GUILayout.Space(2);

            EditorGUILayout.LabelField("Overview", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "Achieve beautiful, lightning-fast volumetric lighting in your project. Built entirely with standard Shader Graph for maximum speed and optimization.\n\n" +
                "<b>Includes:</b>\n" +
                "• Point and Spot light volumetrics\n" +
                "• 10 ready-to-use prefabs\n" +
                "• 9 sample scenes\n" +
                "• Procedural light and camera scripts",
                bodyStyle
            );
            GUILayout.EndVertical();

            EditorGUILayout.LabelField("Quick Start", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "<b>1.</b> Open the Prefab folder.\n" +
                "<b>2.</b> Drag the desired volume into your scene hierarchy — use VP Lit / VP Unlit for point lights, V Cone variants for spotlights.\n" +
                "<b>3.</b> Position the prefab so its origin aligns exactly with your Light source.\n" +
                "<b>4.</b> Scale the transform to match the range and angle of your actual light.\n\n" +
                "<i>See the Documentation tab for material tweaking and the full script toolkit reference.</i>",
                bodyStyle
            );
            GUILayout.EndVertical();

            GUILayout.Space(4);

            // Support & links — equal-width, equal-height columns so they actually align.
            float contentWidth = position.width - 24f;
            float colWidth = (contentWidth - 8f) / 2f;

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(paddedBox, GUILayout.Width(colWidth), GUILayout.Height(78));
            EditorGUILayout.LabelField("<b>Need Help?</b>", bodyStyle);
            EditorGUILayout.LabelField(SupportEmail, bodyStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Documentation", GUILayout.Height(22)))
            {
                if (!string.IsNullOrEmpty(documentationPdfPath))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(documentationPdfPath));
                else
                    currentTab = Tab.Documentation;
            }
            GUILayout.EndVertical();

            GUILayout.Space(8);

            GUILayout.BeginVertical(paddedBox, GUILayout.Width(colWidth), GUILayout.Height(78));
            EditorGUILayout.LabelField("<b>More by MonoLimbo</b>", bodyStyle);
            EditorGUILayout.LabelField("Browse our other assets", bodyStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("View Publisher Page", GUILayout.Height(22)))
                Application.OpenURL(PublisherUrl);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Review ask — gated so it doesn't fire on first-ever open.
            if (openCount >= 2)
            {
                DrawReviewCallToAction();
                GUILayout.Space(10);
            }

            GUILayout.FlexibleSpace();
            DrawFooter();
        }

        private void DrawReviewCallToAction()
        {
            bool dark = EditorGUIUtility.isProSkin;

            // Distinct tinted card so this reads as the standout action on the page, not just another box.
            Color cardBg = dark ? new Color(0.30f, 0.22f, 0.10f) : new Color(0.99f, 0.93f, 0.80f);
            Color accent = dark ? new Color(0.93f, 0.71f, 0.30f) : new Color(0.72f, 0.50f, 0.08f);

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = cardBg;
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = prevBg;

            GUILayout.Space(8);
            var titleStyle = new GUIStyle(centeredBody) { fontStyle = FontStyle.Bold, fontSize = 13 };
            titleStyle.normal.textColor = accent;
            EditorGUILayout.LabelField("★ Enjoying the asset?", titleStyle);
            EditorGUILayout.LabelField("A quick review helps enormously — it's the biggest factor in future updates.", centeredBody);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Color prevBtnBg = GUI.backgroundColor;
            GUI.backgroundColor = accent;
            if (GUILayout.Button("Leave a Review on the Asset Store", GUILayout.Height(30), GUILayout.Width(260)))
                Application.OpenURL(AssetReviewUrl);
            GUI.backgroundColor = prevBtnBg;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            bool newToggle = EditorGUILayout.ToggleLeft("Do not show this window again", dontShowAgain, GUILayout.Width(190));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Got it", GUILayout.Width(80), GUILayout.Height(22)))
                Close();
            GUILayout.EndHorizontal();

            if (newToggle != dontShowAgain)
            {
                dontShowAgain = newToggle;
                if (dontShowAgain)
                    EditorPrefs.SetInt(DontShowKey, 1);
                else
                    EditorPrefs.DeleteKey(DontShowKey);
            }
        }

        // ---- What's New tab --------------------------------------------

        private void DrawWhatsNewTab()
        {
            EditorGUILayout.LabelField("Changelog", headerStyle);

            changelogScroll = EditorGUILayout.BeginScrollView(changelogScroll, GUILayout.ExpandHeight(true));

            foreach (var entry in Changelog)
            {
                GUILayout.BeginVertical(paddedBox);

                GUILayout.BeginHorizontal();
                string badgeLabel = entry.IsCurrent ? "NOW" : entry.IsUpcoming ? "NEXT" : "v" + entry.Version;
                Color prevBg = GUI.backgroundColor;
                if (entry.IsCurrent) GUI.backgroundColor = new Color(0.4f, 0.75f, 0.4f);
                else if (entry.IsUpcoming) GUI.backgroundColor = new Color(0.6f, 0.6f, 0.9f);
                GUILayout.Label(badgeLabel, versionBadge, GUILayout.Width(44));
                GUI.backgroundColor = prevBg;

                GUILayout.Label("v" + entry.Version, EditorStyles.miniLabel, GUILayout.Width(38));
                EditorGUILayout.LabelField($"<b>{entry.Title}</b>", bodyStyle);
                GUILayout.EndHorizontal();

                GUILayout.Space(1);
                EditorGUILayout.LabelField(entry.Blurb, bodyStyle);
                GUILayout.Space(4);

                foreach (var (heading, items) in entry.Sections)
                {
                    EditorGUILayout.LabelField(heading, sectionLabel);
                    foreach (var item in items)
                        EditorGUILayout.LabelField("• " + item, bodyStyle);
                    GUILayout.Space(2);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
        }

        // ---- Documentation tab -----------------------------------------

        private void DrawDocumentationTab()
        {
            docsScroll = EditorGUILayout.BeginScrollView(docsScroll, GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField("Package Contents", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "• <b>Prefabs:</b> 10 ready-to-use prefabs (V Cone, V Cone Lit Flicker, VP Lit, VP Unlit, etc.)\n" +
                "• <b>Scenes:</b> 9 sample scenes (Flash Light, Horror, Street, Party, Passage, etc.)\n" +
                "• <b>Materials:</b> Cone, Point, and Colored materials plus raw shaders and textures\n" +
                "• <b>Bonus FX:</b> Lens Flares (Default, Normal, Red) and ambient/lightbulb SFX\n" +
                "• <b>Bonus Scripts:</b> 5 procedural control scripts for dynamic lights and cameras",
                bodyStyle
            );
            GUILayout.EndVertical();

            EditorGUILayout.LabelField("Tweaking the Visuals", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "Select your volumetric prefab and adjust the Material component:\n\n" +
                "• <b>Opacity & Color</b> — density and tint of the beam\n" +
                "• <b>Edge & Fade Distance</b> — softens intersections with geometry to avoid harsh clipping\n" +
                "• <b>Use Wind? / Noise Settings</b> — Scale, Speed, and Density for scrolling dust, wind, or underwater light scattering",
                bodyStyle
            );
            GUILayout.EndVertical();

            EditorGUILayout.LabelField("Procedural Script Toolkit", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "Found in <b>Bonus &gt; Scripts</b> (MonoLimbo namespace). Attach to your Light/Camera, assign target components, and tune the exposed variables:\n\n" +
                "• <b>LightFlicker</b> — syncs a Light's intensity with material opacity for erratic flickering\n" +
                "• <b>PartyLightController</b> — cycles RGB while flickering; auto-desyncs when duplicated\n" +
                "• <b>PartyLightSwing</b> — sweeping figure-8 stage-light motion with built-in time offsets\n" +
                "• <b>HandheldCameraShake</b> — combines slow breathing sway with fast nervous jitter\n" +
                "• <b>IdleCameraDolly</b> — continuous dolly-zoom (Vertigo effect) via Z-push + inverse FOV",
                bodyStyle
            );
            GUILayout.EndVertical();

            EditorGUILayout.LabelField("Shader Graph Breakdown", headerStyle);
            GUILayout.BeginVertical(paddedBox);
            EditorGUILayout.LabelField(
                "Zero custom nodes — built entirely in standard Shader Graph:\n\n" +
                "• <b>Wind & Extra Noise</b> — panning Noise nodes for atmospheric motion\n" +
                "• <b>Edge Fading</b> — Fresnel logic to soften hard mesh geometry\n" +
                "• <b>Camera & Intersection Fading</b> — Scene Depth / Camera Distance to fade on clipping",
                bodyStyle
            );
            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(documentationPdfPath)))
            {
                string label = string.IsNullOrEmpty(documentationPdfPath) ? "Documentation PDF Not Found" : "Open Documentation PDF";
                if (GUILayout.Button(label, GUILayout.Height(26), GUILayout.Width(220)))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(documentationPdfPath));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }
    }
}