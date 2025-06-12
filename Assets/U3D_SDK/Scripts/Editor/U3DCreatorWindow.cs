using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace U3D.Editor
{
    public class U3DCreatorWindow : EditorWindow
    {
        [SerializeField] private int selectedTab = 0;
        [SerializeField] private Vector2 scrollPosition;

        private List<ICreatorTab> tabs;
        private GUIStyle headerStyle;
        private GUIStyle tabButtonStyle;
        private GUIStyle activeTabButtonStyle;
        private bool stylesInitialized = false;
        private Texture2D logoTexture;

        [MenuItem("U3D/Creator Dashboard")]
        public static void ShowWindow()
        {
            var window = GetWindow<U3DCreatorWindow>("U3D Creator Dashboard");
            window.minSize = new Vector2(320, 500);
            window.Show();
        }

        [InitializeOnLoadMethod]
        static void OpenOnStartup()
        {
            EditorApplication.delayCall += () => {
                if (EditorPrefs.GetBool("U3D_ShowOnStartup", true))
                {
                    ShowWindow();
                }
            };
        }

        void OnEnable()
        {
            InitializeTabs();
            LoadLogo();
        }

        void LoadLogo()
        {
            string[] guids = AssetDatabase.FindAssets("U3D512Logo t:Texture2D");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        void InitializeTabs()
        {
            tabs = new List<ICreatorTab>
            {
                new SetupTab(),
                new ProjectToolsTab(),
                new CheckFixTab(),
                new PublishTab()
            };

            foreach (var tab in tabs)
            {
                tab.Initialize();
            }
        }

        void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.4f, 0.5f, 0.9f) },
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            tabButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 12,
                fixedHeight = 35,
                normal = {
                    textColor = Color.white,
                    background = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f))
                },
                hover = {
                    background = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.4f))
                }
            };

            activeTabButtonStyle = new GUIStyle(tabButtonStyle)
            {
                normal = {
                    textColor = Color.white,
                    background = MakeTex(1, 1, new Color(0.4f, 0.5f, 0.9f))
                }
            };

            stylesInitialized = true;
        }

        void OnGUI()
        {
            InitializeStyles();
            DrawLogoFirst();
            DrawRestOfHeader();
            DrawTabNavigation();
            DrawCurrentTab();
        }

        void DrawLogoFirst()
        {
            if (logoTexture != null)
            {
                float windowWidth = position.width;
                float logoSize = Mathf.Min(512f, windowWidth * 0.8f);

                // Draw at absolute top of window content area
                Rect logoRect = new Rect((windowWidth - logoSize) * 0.5f, 0, logoSize, logoSize);
                GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit);

                // Advance layout cursor past the logo
                GUILayout.Space(logoSize);
            }
        }

        void DrawRestOfHeader()
        {
            float windowWidth = position.width;

            // Responsive header text
            string headerText;
            float textHeight;

            if (windowWidth < 550)
            {
                headerText = "Unity 6 powered publishing\n+\nmonetization made instant";
                textHeight = 60f;
            }
            else
            {
                headerText = "Unity 6 powered publishing + monetization made instant";
                textHeight = 20f;
            }

            EditorGUILayout.LabelField(headerText, headerStyle, GUILayout.Height(textHeight));
            EditorGUILayout.Space(6);

            // Environment label
            var envColor = GUI.color;
            GUI.color = new Color(0.2f, 0.8f, 0.2f);
            EditorGUILayout.LabelField("✓ Connected to Production Environment", EditorStyles.centeredGreyMiniLabel);
            GUI.color = envColor;

            EditorGUILayout.Space(8);
        }


        void DrawTabNavigation()
        {
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < tabs.Count; i++)
            {
                var tabStyle = selectedTab == i ? activeTabButtonStyle : tabButtonStyle;
                var tabName = tabs[i].TabName;

                if (tabs[i].IsComplete)
                {
                    tabName = "✓ " + tabName;
                }

                if (GUILayout.Button(tabName, tabStyle))
                {
                    selectedTab = i;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        void DrawCurrentTab()
        {
            if (tabs != null && selectedTab < tabs.Count)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                tabs[selectedTab].DrawTab();
                EditorGUILayout.EndScrollView();
            }
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}