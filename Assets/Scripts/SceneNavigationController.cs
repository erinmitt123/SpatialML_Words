using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.UI;
using ByteDance.PICO.XR;
using PicoXR.SpatialML.Demo.Spatial;
using TMPro;
using SpatialButton = UnityEngine.UI.Button;
using SpatialImage = UnityEngine.UI.Image;
using SpatialText = TMPro.TextMeshProUGUI;
using ToolkitButton = UnityEngine.UIElements.Button;

namespace PicoXR.SpatialML.Demo
{
    [DisallowMultipleComponent]
    public sealed class SceneNavigationController : MonoBehaviour
    {
        private const string k_logTag = "[SceneNavigation]";
        private const string k_xrLoaderScenePath = "Assets/Scenes/SceneLoader.unity";
        private const string k_spatialLoaderScenePath =
            "Assets/Scenes/Spatial/SpatialSceneLoader.unity";
        private const string k_spatialSceneRoot = "Assets/Scenes/Spatial/";
        private const string k_runtimeThemeResourcePath = "SceneNavigationRuntimeTheme";
        private const float k_panelDistanceInMeters = 1.5f;
        private const float k_panelWorldScale = 0.12f;
        private const float k_navigationThreshold = 0.6f;
        private const float k_navigationRepeatDelayInSeconds = 0.2f;
        private const float k_backButtonLeftInPixels = 32f;
        private const float k_backButtonTopInPixels = 32f;
        private const float k_backButtonWidthInPixels = 260f;
        private const float k_backButtonHeightInPixels = 58f;
        private const int k_panelWidthInPixels = 960;
        private const int k_panelHeightInPixels = 720;

        private static readonly Color s_buttonColor = new(0.18f, 0.28f, 0.52f, 0.98f);
        private static readonly Color s_selectedButtonColor = new(0.28f, 0.48f, 0.88f, 1f);
        private static readonly Color s_styleButtonFrameColor = new(0.04f, 0.06f, 0.12f, 1f);
        private static SceneNavigationController s_instance;

        private readonly List<ToolkitButton> m_toolkitNavigationButtons = new(8);
        private readonly List<SpatialButton> m_spatialNavigationButtons = new(8);
        private readonly List<Action> m_navigationActions = new(8);

        [Header("Spatial UI")]
        [Tooltip("Serialized uGUI Canvas exported and synchronized by Spatial Adapter.")]
        [SerializeField] private Canvas m_spatialCanvas;
        [SerializeField] private GameObject m_spatialSceneSelectorRoot;
        [SerializeField] private SpatialText m_spatialStatusLabel;
        [SerializeField] private SpatialButton[] m_spatialSceneButtons =
            Array.Empty<SpatialButton>();
        [Tooltip("Dedicated square buttons authored for the portal style picker.")]
        [SerializeField] private SpatialButton[] m_spatialStyleButtons =
            Array.Empty<SpatialButton>();
        [Tooltip("Authored sprite Images exported with the loader scene for the portal style picker.")]
        [SerializeField] private SpatialImage[] m_spatialStyleThumbnails =
            Array.Empty<SpatialImage>();
        [SerializeField] private SpatialButton m_spatialBackButton;

        private UIDocument m_uiDocument;
        private PanelSettings m_panelSettings;
        private BoxCollider m_panelCollider;
        private VisualElement m_root;
        private InputAction m_navigateAction;
        private InputAction m_submitAction;
        private InputAction m_cancelAction;
        private InputAction m_systemBackAction;
        private Camera m_mainCamera;
        private bool m_isLoading;
        private bool m_isQuitting;
        private bool m_isApplicationPaused;
        private bool m_shouldLoadLoaderAfterResume;
        private bool m_isNavigationHeld;
        private int m_selectedButtonIndex;
        private float m_nextNavigationTime;
        private Vector2[] m_defaultSpatialButtonPositions = Array.Empty<Vector2>();
        private Vector2[] m_defaultSpatialButtonSizes = Array.Empty<Vector2>();
        private Vector3 m_defaultSpatialCanvasLocalPosition;
        private Vector2 m_defaultSpatialSelectorPosition;
        private Vector2 m_defaultSpatialSelectorSize;
        private Color m_defaultSpatialSelectorColor;
        private Vector2 m_defaultSpatialTitlePosition;
        private Vector2 m_defaultSpatialStatusPosition;
        private StylizePortalSpatialSample m_activeStyleSample;
        private Task<int> m_activeCleanupTask;
        private bool m_usesSpatialUi;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            m_usesSpatialUi = m_spatialCanvas != null;
            CacheSpatialButtonAppearance();
            if (!m_usesSpatialUi)
            {
                PXR_Manager.EnableVideoSeeThrough = true;
                CreateDocument();
            }

            CreateInputActions();
            Application.wantsToQuit += HandleApplicationWantsToQuit;
        }

        private void Start()
        {
            // SceneManager.sceneLoaded is not guaranteed to be observed for the initial scene.
            RebuildUi(SceneManager.GetActiveScene());
            PositionPanelInFrontOfCamera();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            PXR_Plugin.System.FocusStateLost += HandlePicoFocusStateLost;
            PXR_Plugin.System.FocusStateAcquired += HandlePicoFocusStateAcquired;
            PXR_Plugin.System.SessionStateChanged += HandlePicoSessionStateChanged;
            m_navigateAction?.Enable();
            m_submitAction?.Enable();
            m_cancelAction?.Enable();
            m_systemBackAction?.Enable();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            PXR_Plugin.System.FocusStateLost -= HandlePicoFocusStateLost;
            PXR_Plugin.System.FocusStateAcquired -= HandlePicoFocusStateAcquired;
            PXR_Plugin.System.SessionStateChanged -= HandlePicoSessionStateChanged;
            m_navigateAction?.Disable();
            m_submitAction?.Disable();
            m_cancelAction?.Disable();
            m_systemBackAction?.Disable();
        }

        private void OnDestroy()
        {
            if (s_instance != this)
            {
                return;
            }

            Application.wantsToQuit -= HandleApplicationWantsToQuit;
            m_navigateAction?.Dispose();
            m_submitAction?.Dispose();
            m_cancelAction?.Dispose();
            m_systemBackAction?.Dispose();
            SetActiveStyleSample(null);

            if (m_panelSettings != null)
            {
                Destroy(m_panelSettings);
            }

            s_instance = null;
        }

        private void Update()
        {
            if (m_isLoading)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!IsLoaderScene(activeScene) &&
                (m_cancelAction.WasPressedThisFrame() ||
                 m_systemBackAction.WasPressedThisFrame()))
            {
                LoadLoaderScene();
                return;
            }

            if (m_navigationActions.Count == 0)
            {
                return;
            }

            // Authored uGUI controls receive Spatial pointer and navigation events through the
            // persistent EventSystem. Avoid processing the same controller input a second time.
            if (m_usesSpatialUi)
            {
                return;
            }

            HandleNavigationInput();

            if (m_submitAction.WasPressedThisFrame())
            {
                m_navigationActions[m_selectedButtonIndex].Invoke();
            }
        }

        private void LateUpdate()
        {
            if (!m_usesSpatialUi)
            {
                PositionPanelInFrontOfCamera();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            m_isApplicationPaused = isPaused;
            Scene activeScene = SceneManager.GetActiveScene();
            Debug.Log($"{k_logTag} Application pause changed: isPaused={isPaused}, scene={activeScene.name}");

            if (isPaused)
            {
                PrepareForBackground("Application pause");
                return;
            }

            LoadLoaderAfterBackgroundIfNeeded();
        }

        private void CreateDocument()
        {
            m_panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            m_panelSettings.name = "Runtime Scene Navigation Panel";
            m_panelSettings.renderMode = PanelRenderMode.WorldSpace;
            m_panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            m_panelSettings.sortingOrder = 1000;
            m_panelSettings.themeStyleSheet =
                Resources.Load<ThemeStyleSheet>(k_runtimeThemeResourcePath);

            if (m_panelSettings.themeStyleSheet == null)
            {
                Debug.LogError(
                    $"Scene navigation theme is missing from Resources: {k_runtimeThemeResourcePath}");
            }

            m_uiDocument = gameObject.AddComponent<UIDocument>();
            m_uiDocument.panelSettings = m_panelSettings;
            m_uiDocument.sortingOrder = 1000;
            m_uiDocument.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            m_uiDocument.worldSpaceSize = new Vector2(k_panelWidthInPixels, k_panelHeightInPixels);

            m_panelCollider = gameObject.AddComponent<BoxCollider>();
            m_panelCollider.isTrigger = true;

            var inputConfiguration = gameObject.AddComponent<PanelInputConfiguration>();
            inputConfiguration.processWorldSpaceInput = true;
            inputConfiguration.panelInputRedirection = PanelInputConfiguration.PanelInputRedirection.Never;
            inputConfiguration.defaultEventCameraIsMainCamera = true;
            inputConfiguration.autoCreatePanelComponents = true;
            inputConfiguration.interactionLayers = ~0;

            gameObject.AddComponent<XRUIToolkitManager>();
            transform.localScale = Vector3.one * k_panelWorldScale;

            m_root = m_uiDocument.rootVisualElement;
            m_root.name = "scene-navigation-root";
            m_root.pickingMode = PickingMode.Ignore;
            m_root.style.position = Position.Absolute;
            m_root.style.left = 0;
            m_root.style.top = 0;
            m_root.style.right = 0;
            m_root.style.bottom = 0;
        }

        private void CreateInputActions()
        {
            m_navigateAction = new InputAction(
                "Scene Navigation",
                InputActionType.Value,
                expectedControlType: "Vector2");
            m_navigateAction.AddBinding("<XRController>{LeftHand}/primary2DAxis");
            m_navigateAction.AddBinding("<XRController>{RightHand}/primary2DAxis");
            m_navigateAction.AddBinding("<Gamepad>/leftStick");
            m_navigateAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            m_submitAction = new InputAction("Select Scene", InputActionType.Button);
            m_submitAction.AddBinding("<XRController>{LeftHand}/triggerButton");
            m_submitAction.AddBinding("<XRController>{RightHand}/triggerButton");
            m_submitAction.AddBinding("<XRController>{LeftHand}/primary2DAxisClick");
            m_submitAction.AddBinding("<XRController>{RightHand}/primary2DAxisClick");
            m_submitAction.AddBinding("<Gamepad>/buttonSouth");
            m_submitAction.AddBinding("<Keyboard>/enter");
            m_submitAction.AddBinding("<Keyboard>/numpadEnter");

            m_cancelAction = new InputAction("Back to Scene Loader", InputActionType.Button);
            m_cancelAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
            m_cancelAction.AddBinding("<XRController>{RightHand}/secondaryButton");
            m_cancelAction.AddBinding("<Gamepad>/buttonEast");
            m_cancelAction.AddBinding("<Keyboard>/escape");

            m_systemBackAction = new InputAction("System Back", InputActionType.Button);
            m_systemBackAction.AddBinding("<Keyboard>/escape");
            m_systemBackAction.AddBinding("<XRController>{LeftHand}/menuButton");
            m_systemBackAction.AddBinding("<XRController>{RightHand}/menuButton");
            m_systemBackAction.AddBinding("<PICO4TouchController>{LeftHand}/menu");
            m_systemBackAction.AddBinding("<PICO4UltraController>{LeftHand}/menu");
            m_systemBackAction.AddBinding("<PICONeo3Controller>{LeftHand}/menu");
            m_systemBackAction.AddBinding("<PICOG3TouchController>/menu");
        }

        private bool HandleApplicationWantsToQuit()
        {
            if (m_isQuitting)
            {
                return true;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (!IsLoaderScene(activeScene))
            {
                Debug.Log(
                    $"{k_logTag} Quit requested from {activeScene.name}; " +
                    "draining SpatialML owner(s) before returning to SceneLoader.");
                m_shouldLoadLoaderAfterResume = false;
                LoadLoaderScene();
                return false;
            }

            m_isQuitting = true;
            Debug.Log($"{k_logTag} Quit requested from SceneLoader.");
            return true;
        }

        private void HandlePicoFocusStateLost()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Debug.Log($"{k_logTag} PICO XR focus lost in scene={activeScene.name}");
            PrepareForBackground("PICO XR focus loss");
        }

        private void HandlePicoFocusStateAcquired()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Debug.Log($"{k_logTag} PICO XR focus acquired in scene={activeScene.name}");

            if (!m_isApplicationPaused)
            {
                LoadLoaderAfterBackgroundIfNeeded();
            }
        }

        private void HandlePicoSessionStateChanged(XrSessionState sessionState)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Debug.Log(
                $"{k_logTag} PICO XR session changed: " +
                $"state={sessionState}, scene={activeScene.name}");

            if (sessionState == XrSessionState.Focused)
            {
                if (!m_isApplicationPaused)
                {
                    LoadLoaderAfterBackgroundIfNeeded();
                }

                return;
            }

            if (sessionState == XrSessionState.Visible ||
                sessionState == XrSessionState.Stopping ||
                sessionState == XrSessionState.LossPending ||
                sessionState == XrSessionState.Exiting)
            {
                PrepareForBackground($"PICO XR session {sessionState}");
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"{k_logTag} Loaded scene: {scene.name} ({scene.path}), mode={mode}");
            m_isLoading = false;
            m_activeCleanupTask = null;
            m_mainCamera = null;
            RebuildUi(scene);
            PositionPanelInFrontOfCamera();
        }

        private void RebuildUi(Scene activeScene)
        {
            m_toolkitNavigationButtons.Clear();
            m_spatialNavigationButtons.Clear();
            m_navigationActions.Clear();
            m_selectedButtonIndex = 0;
            m_isNavigationHeld = false;

            if (m_usesSpatialUi)
            {
                RebuildSpatialUi(activeScene);
                return;
            }

            m_root.Clear();
            m_root.SetEnabled(true);

            if (IsLoaderScene(activeScene))
            {
                ConfigureLoaderCollider();
                BuildSceneList(activeScene);
                return;
            }

            ConfigureBackButtonCollider();
            BuildBackButton();
        }

        private void RebuildSpatialUi(Scene activeScene)
        {
            bool showSceneSelector = IsSpatialLoaderScene(activeScene);
            StylizePortalSpatialSample styleSample = showSceneSelector
                ? null
                : FindFirstObjectByType<StylizePortalSpatialSample>();
            bool showStyleSelector = styleSample != null;
            SetActiveStyleSample(styleSample);
            m_spatialCanvas.gameObject.SetActive(true);
            ConfigureSpatialSelectorLayout(showStyleSelector);
            m_spatialSceneSelectorRoot?.SetActive(showSceneSelector || showStyleSelector);

            SpatialText title = m_spatialSceneSelectorRoot != null
                ? m_spatialSceneSelectorRoot.transform.Find("Title")?.GetComponent<SpatialText>()
                : null;
            if (showSceneSelector && title != null)
            {
                title.text = "SpatialML Spatial Samples";
            }

            if (m_spatialBackButton != null)
            {
                m_spatialBackButton.gameObject.SetActive(!showSceneSelector);
                m_spatialBackButton.onClick.RemoveAllListeners();

                if (!showSceneSelector)
                {
                    ConfigureSpatialSceneButton(
                        m_spatialBackButton,
                        "←  Back to Spatial Samples",
                        LoadLoaderScene);
                    RegisterSpatialNavigationButton(m_spatialBackButton, LoadLoaderScene);
                    SetSelectedButtonIndex(0);
                    FocusSelectedButton();
                }
            }

            if (showStyleSelector)
            {
                for (int index = 0; index < m_spatialSceneButtons.Length; index++)
                {
                    ConfigureUnusedSpatialSceneButton(m_spatialSceneButtons[index]);
                }

                BuildSpatialStyleSelector(styleSample);
                return;
            }

            for (int index = 0; index < m_spatialStyleButtons.Length; index++)
            {
                ConfigureUnusedSpatialSceneButton(m_spatialStyleButtons[index]);
            }

            if (!showSceneSelector)
            {
                return;
            }

            RestoreSpatialButtonAppearance();

            int buttonIndex = 0;
            int sceneCount = SceneManager.sceneCountInBuildSettings;

            for (int buildIndex = 0; buildIndex < sceneCount; buildIndex++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
                if (IsLoaderScenePath(scenePath) || !IsSpatialScenePath(scenePath))
                {
                    continue;
                }

                if (buttonIndex >= m_spatialSceneButtons.Length)
                {
                    Debug.LogWarning(
                        $"{k_logTag} Spatial selector has {m_spatialSceneButtons.Length} " +
                        $"button slots; additional scene was not shown: {scenePath}");
                    continue;
                }

                SpatialButton sceneButton = m_spatialSceneButtons[buttonIndex];
                int selectedBuildIndex = buildIndex;
                Action loadSelectedScene = () => LoadScene(selectedBuildIndex);
                ConfigureSpatialSceneButton(
                    sceneButton,
                    GetSceneDisplayName(scenePath),
                    loadSelectedScene);
                RegisterSpatialNavigationButton(sceneButton, loadSelectedScene);
                buttonIndex++;
            }

            for (int index = buttonIndex; index < m_spatialSceneButtons.Length; index++)
            {
                ConfigureUnusedSpatialSceneButton(m_spatialSceneButtons[index]);
            }

            if (m_spatialStatusLabel != null)
            {
                m_spatialStatusLabel.text = buttonIndex == 0
                    ? "No enabled Spatial sample scenes were found in Build Settings."
                    : "Select a Spatial scene to run";
                m_spatialStatusLabel.color = buttonIndex == 0
                    ? new Color(1.0f, 0.67f, 0.3f)
                    : new Color(0.7f, 0.75f, 0.86f);
            }

            if (buttonIndex > 0)
            {
                SetSelectedButtonIndex(0);
                FocusSelectedButton();
            }
        }

        private static void ConfigureSpatialSceneButton(
            SpatialButton button,
            string sceneName,
            Action loadScene)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(loadScene.Invoke);
            button.gameObject.SetActive(true);
            button.interactable = true;
            if (button.image != null)
            {
                button.targetGraphic = button.image;
                // Spatial Adapter synchronizes Image.color; the Selectable's
                // CanvasRenderer tint alone does not set the native background.
                // Keep the blue fill here and let the native hover effect highlight it.
                button.image.color = s_buttonColor;
            }

            SpatialImage thumbnail = FindStyleThumbnail(button);
            if (thumbnail != null)
            {
                thumbnail.gameObject.SetActive(false);
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.2f, 0.38f, 0.76f, 1f);
            colors.disabledColor = Color.clear;
            button.colors = colors;

            SpatialText label = button.GetComponentInChildren<SpatialText>(true);
            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.text = sceneName;
                label.color = Color.white;
            }
        }

        private static void ConfigureUnusedSpatialSceneButton(SpatialButton button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = false;
            button.gameObject.SetActive(false);

            var colors = button.colors;
            colors.disabledColor = Color.clear;
            button.colors = colors;

            SpatialText label = button.GetComponentInChildren<SpatialText>(true);
            if (label != null)
            {
                label.text = string.Empty;
                label.color = Color.clear;
            }
        }

        private void BuildSpatialStyleSelector(StylizePortalSpatialSample styleSample)
        {
            SpatialText title = m_spatialSceneSelectorRoot != null
                ? m_spatialSceneSelectorRoot.transform.Find("Title")?.GetComponent<SpatialText>()
                : null;
            if (title != null)
            {
                title.text = "Choose Portal Style";
            }

            if (m_spatialStatusLabel != null)
            {
                m_spatialStatusLabel.text = "Select a texture to restyle the portal";
                m_spatialStatusLabel.color = new Color(0.7f, 0.75f, 0.86f);
            }

            int styleCount = Mathf.Min(
                styleSample.StyleCount,
                m_spatialStyleButtons.Length,
                m_spatialStyleThumbnails.Length);

            for (int index = 0; index < styleCount; index++)
            {
                SpatialButton button = m_spatialStyleButtons[index];
                SpatialImage thumbnail = m_spatialStyleThumbnails[index];
                int selectedStyleIndex = index;
                Action selectStyle = () => styleSample.SelectStyle(selectedStyleIndex);

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(selectStyle.Invoke);
                button.gameObject.SetActive(true);
                button.interactable = true;

                if (thumbnail != null)
                {
                    // Keep the authored Sprite and square RectTransform. Spatial Adapter exports
                    // UI geometry at build time, so these dedicated buttons must not reuse the
                    // loader's long scene-button geometry.
                    thumbnail.color = Color.white;
                    thumbnail.preserveAspect = true;
                    thumbnail.raycastTarget = true;
                    thumbnail.gameObject.SetActive(true);
                    button.targetGraphic = thumbnail;
                }

                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.72f, 0.86f, 1.0f, 1.0f);
                colors.selectedColor = new Color(0.72f, 0.86f, 1.0f, 1.0f);
                colors.pressedColor = new Color(0.72f, 0.8f, 1.0f, 1.0f);
                colors.disabledColor = Color.clear;
                button.colors = colors;

                SpatialText label = button.GetComponentInChildren<SpatialText>(true);
                if (label != null)
                {
                    label.text = string.Empty;
                    label.gameObject.SetActive(false);
                }

                RegisterSpatialNavigationButton(button, selectStyle);
            }

            for (int index = styleCount; index < m_spatialStyleButtons.Length; index++)
            {
                ConfigureUnusedSpatialSceneButton(m_spatialStyleButtons[index]);
            }

            if (styleCount > 0)
            {
                UpdateSpatialStyleSelection(styleSample.CurrentStyleIndex);
                SetSelectedButtonIndex(Mathf.Clamp(styleSample.CurrentStyleIndex + 1, 1, styleCount));
                FocusSelectedButton();
            }
        }

        private void SetActiveStyleSample(StylizePortalSpatialSample styleSample)
        {
            if (m_activeStyleSample != null)
            {
                m_activeStyleSample.StyleSelected -= HandleSpatialStyleSelected;
            }

            m_activeStyleSample = styleSample;
            if (m_activeStyleSample != null)
            {
                m_activeStyleSample.StyleSelected += HandleSpatialStyleSelected;
            }
        }

        private void HandleSpatialStyleSelected(int styleIndex)
        {
            UpdateSpatialStyleSelection(styleIndex);
            SetSelectedButtonIndex(styleIndex + 1);
            FocusSelectedButton();
        }

        private void UpdateSpatialStyleSelection(int selectedStyleIndex)
        {
            int styleCount = Mathf.Min(m_spatialStyleButtons.Length, m_spatialStyleThumbnails.Length);
            for (int index = 0; index < styleCount; index++)
            {
                SpatialImage selectionFrame = m_spatialStyleButtons[index]?.image;
                if (selectionFrame != null)
                {
                    // A transparent parent Image can hide its child thumbnail in
                    // Spatial Adapter. Keep every style frame opaque, selected or not.
                    selectionFrame.color = index == selectedStyleIndex
                        ? s_selectedButtonColor
                        : s_styleButtonFrameColor;
                }
            }
        }

        private void CacheSpatialButtonAppearance()
        {
            if (!m_usesSpatialUi || m_spatialSceneButtons == null)
            {
                return;
            }

            int count = m_spatialSceneButtons.Length;
            m_defaultSpatialButtonPositions = new Vector2[count];
            m_defaultSpatialButtonSizes = new Vector2[count];
            m_defaultSpatialCanvasLocalPosition = m_spatialCanvas.transform.localPosition;

            if (m_spatialSceneSelectorRoot != null)
            {
                RectTransform selectorRect =
                    m_spatialSceneSelectorRoot.GetComponent<RectTransform>();
                m_defaultSpatialSelectorPosition = selectorRect.anchoredPosition;
                m_defaultSpatialSelectorSize = selectorRect.sizeDelta;

                SpatialImage selectorImage =
                    m_spatialSceneSelectorRoot.GetComponent<SpatialImage>();
                if (selectorImage != null)
                {
                    m_defaultSpatialSelectorColor = selectorImage.color;
                }

                RectTransform titleRect = m_spatialSceneSelectorRoot.transform
                    .Find("Title")?.GetComponent<RectTransform>();
                RectTransform statusRect = m_spatialSceneSelectorRoot.transform
                    .Find("Status")?.GetComponent<RectTransform>();
                if (titleRect != null)
                {
                    m_defaultSpatialTitlePosition = titleRect.anchoredPosition;
                }

                if (statusRect != null)
                {
                    m_defaultSpatialStatusPosition = statusRect.anchoredPosition;
                }
            }

            for (int index = 0; index < count; index++)
            {
                SpatialButton button = m_spatialSceneButtons[index];
                if (button == null)
                {
                    continue;
                }

                RectTransform rect = button.GetComponent<RectTransform>();
                m_defaultSpatialButtonPositions[index] = rect.anchoredPosition;
                m_defaultSpatialButtonSizes[index] = rect.sizeDelta;
            }
        }

        private void ConfigureSpatialSelectorLayout(bool showStyleSelector)
        {
            Vector3 canvasPosition = m_defaultSpatialCanvasLocalPosition;
            canvasPosition.z = showStyleSelector ? 0.08f : m_defaultSpatialCanvasLocalPosition.z;
            m_spatialCanvas.transform.localPosition = canvasPosition;

            if (m_spatialSceneSelectorRoot == null)
            {
                return;
            }

            RectTransform selectorRect =
                m_spatialSceneSelectorRoot.GetComponent<RectTransform>();
            SpatialImage selectorImage =
                m_spatialSceneSelectorRoot.GetComponent<SpatialImage>();
            RectTransform titleRect = m_spatialSceneSelectorRoot.transform
                .Find("Title")?.GetComponent<RectTransform>();
            RectTransform statusRect = m_spatialSceneSelectorRoot.transform
                .Find("Status")?.GetComponent<RectTransform>();

            selectorRect.anchoredPosition = m_defaultSpatialSelectorPosition;
            selectorRect.sizeDelta = m_defaultSpatialSelectorSize;

            if (selectorImage != null)
            {
                selectorImage.color = showStyleSelector
                    ? new Color(0.02f, 0.025f, 0.05f, 0.18f)
                    : m_defaultSpatialSelectorColor;
            }

            if (titleRect != null)
            {
                titleRect.anchoredPosition = showStyleSelector
                    ? new Vector2(110.0f, 300.0f)
                    : m_defaultSpatialTitlePosition;
            }

            if (statusRect != null)
            {
                statusRect.anchoredPosition = showStyleSelector
                    ? new Vector2(110.0f, 260.0f)
                    : m_defaultSpatialStatusPosition;
            }
        }

        private void RestoreSpatialButtonAppearance()
        {
            for (int index = 0; index < m_spatialSceneButtons.Length; index++)
            {
                SpatialButton button = m_spatialSceneButtons[index];
                if (button == null)
                {
                    continue;
                }

                SpatialImage image = button.image;
                if (image != null)
                {
                    button.targetGraphic = image;
                    image.color = Color.white;
                }

                SpatialImage thumbnail = FindStyleThumbnail(button);
                if (thumbnail != null)
                {
                    thumbnail.gameObject.SetActive(false);
                }

                SpatialText label = button.GetComponentInChildren<SpatialText>(true);
                if (label != null)
                {
                    label.gameObject.SetActive(true);
                }

                RectTransform rect = button.GetComponent<RectTransform>();
                if (index < m_defaultSpatialButtonPositions.Length)
                {
                    rect.anchoredPosition = m_defaultSpatialButtonPositions[index];
                    rect.sizeDelta = m_defaultSpatialButtonSizes[index];
                }
            }
        }

        private static SpatialImage FindStyleThumbnail(SpatialButton button)
        {
            if (button == null)
            {
                return null;
            }

            Transform thumbnailTransform = button.transform.Find("Style Thumbnail");
            return thumbnailTransform != null
                ? thumbnailTransform.GetComponent<SpatialImage>()
                : null;
        }

        private void BuildSceneList(Scene loaderScene)
        {
            bool listSpatialScenes = IsSpatialLoaderScene(loaderScene);
            var overlay = new VisualElement
            {
                name = "scene-loader-overlay",
                pickingMode = PickingMode.Position,
            };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = new Color(0.025f, 0.035f, 0.07f, 0.94f);
            m_root.Add(overlay);

            var card = new VisualElement
            {
                name = "scene-loader-card",
            };
            card.style.width = 720;
            card.style.maxWidth = Length.Percent(90);
            card.style.maxHeight = Length.Percent(86);
            card.style.paddingLeft = 36;
            card.style.paddingTop = 32;
            card.style.paddingRight = 36;
            card.style.paddingBottom = 32;
            card.style.backgroundColor = new Color(0.07f, 0.09f, 0.16f, 0.98f);
            card.style.borderTopLeftRadius = 24;
            card.style.borderTopRightRadius = 24;
            card.style.borderBottomLeftRadius = 24;
            card.style.borderBottomRightRadius = 24;
            overlay.Add(card);

            var title = new Label(
                listSpatialScenes ? "SpatialML Spatial Samples" : "SpatialML XR Samples");
            title.style.fontSize = 38;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 8;
            card.Add(title);

            var subtitle = new Label(
                listSpatialScenes ? "Select a Spatial scene to run" : "Select an XR scene to run");
            subtitle.style.fontSize = 20;
            subtitle.style.color = new Color(0.7f, 0.75f, 0.86f);
            subtitle.style.marginBottom = 24;
            card.Add(subtitle);

            var sceneList = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "build-scene-list",
            };
            sceneList.style.flexGrow = 1;
            card.Add(sceneList);

            ToolkitButton firstSceneButton = null;
            int sceneCount = SceneManager.sceneCountInBuildSettings;

            for (int buildIndex = 0; buildIndex < sceneCount; buildIndex++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
                if (IsLoaderScenePath(scenePath) ||
                    IsSpatialScenePath(scenePath) != listSpatialScenes)
                {
                    continue;
                }

                int selectedBuildIndex = buildIndex;
                string sceneName = GetSceneDisplayName(scenePath);
                Action loadSelectedScene = () => LoadScene(selectedBuildIndex);
                var sceneButton = CreateButton(sceneName, loadSelectedScene);
                sceneButton.tooltip = scenePath;
                sceneList.Add(sceneButton);
                RegisterNavigationButton(sceneButton, loadSelectedScene);
                firstSceneButton ??= sceneButton;
            }

            if (firstSceneButton == null)
            {
                string modeName = listSpatialScenes ? "Spatial" : "XR";
                var emptyMessage = new Label(
                    $"No enabled {modeName} sample scenes were found in Build Settings.");
                emptyMessage.style.fontSize = 18;
                emptyMessage.style.color = new Color(1.0f, 0.67f, 0.3f);
                emptyMessage.style.whiteSpace = WhiteSpace.Normal;
                sceneList.Add(emptyMessage);
                return;
            }

            SetSelectedButtonIndex(0);
            firstSceneButton.schedule.Execute(firstSceneButton.Focus);
        }

        private void BuildBackButton()
        {
            var backButton = CreateButton("←  Back to Samples", LoadLoaderScene);
            backButton.name = "back-to-scene-loader";
            backButton.style.position = Position.Absolute;
            backButton.style.left = k_backButtonLeftInPixels;
            backButton.style.top = k_backButtonTopInPixels;
            backButton.style.width = k_backButtonWidthInPixels;
            backButton.style.height = k_backButtonHeightInPixels;
            m_root.Add(backButton);
            RegisterNavigationButton(backButton, LoadLoaderScene);
            SetSelectedButtonIndex(0);
            backButton.schedule.Execute(backButton.Focus);
        }

        private static ToolkitButton CreateButton(string text, Action clicked)
        {
            var button = new ToolkitButton(clicked)
            {
                text = text,
            };
            button.style.height = 68;
            button.style.marginBottom = 14;
            button.style.paddingLeft = 22;
            button.style.paddingRight = 22;
            button.style.fontSize = 22;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.color = Color.white;
            button.style.backgroundColor = s_buttonColor;
            button.style.borderTopLeftRadius = 12;
            button.style.borderTopRightRadius = 12;
            button.style.borderBottomLeftRadius = 12;
            button.style.borderBottomRightRadius = 12;
            return button;
        }

        private void RegisterNavigationButton(ToolkitButton button, Action action)
        {
            int buttonIndex = m_toolkitNavigationButtons.Count;
            m_toolkitNavigationButtons.Add(button);
            m_navigationActions.Add(action);
            button.RegisterCallback<FocusInEvent>(_ => SetSelectedButtonIndex(buttonIndex));
            button.RegisterCallback<PointerEnterEvent>(_ => SetSelectedButtonIndex(buttonIndex));
        }

        private void RegisterSpatialNavigationButton(SpatialButton button, Action action)
        {
            if (button == null)
            {
                return;
            }

            m_spatialNavigationButtons.Add(button);
            m_navigationActions.Add(action);
        }

        private void HandleNavigationInput()
        {
            float verticalInput = m_navigateAction.ReadValue<Vector2>().y;
            if (Mathf.Abs(verticalInput) < k_navigationThreshold)
            {
                m_isNavigationHeld = false;
                return;
            }

            if (m_isNavigationHeld && Time.unscaledTime < m_nextNavigationTime)
            {
                return;
            }

            int direction = verticalInput > 0f ? -1 : 1;
            int nextButtonIndex = m_selectedButtonIndex + direction;

            if (nextButtonIndex < 0)
            {
                nextButtonIndex = m_navigationActions.Count - 1;
            }
            else if (nextButtonIndex >= m_navigationActions.Count)
            {
                nextButtonIndex = 0;
            }

            SetSelectedButtonIndex(nextButtonIndex);
            FocusSelectedButton();
            m_isNavigationHeld = true;
            m_nextNavigationTime = Time.unscaledTime + k_navigationRepeatDelayInSeconds;
        }

        private void SetSelectedButtonIndex(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= m_navigationActions.Count)
            {
                return;
            }

            if (!m_usesSpatialUi && m_selectedButtonIndex < m_toolkitNavigationButtons.Count)
            {
                m_toolkitNavigationButtons[m_selectedButtonIndex].style.backgroundColor =
                    s_buttonColor;
            }

            m_selectedButtonIndex = buttonIndex;

            if (!m_usesSpatialUi)
            {
                m_toolkitNavigationButtons[m_selectedButtonIndex].style.backgroundColor =
                    s_selectedButtonColor;
            }
        }

        private void FocusSelectedButton()
        {
            if (m_usesSpatialUi)
            {
                if (m_selectedButtonIndex < m_spatialNavigationButtons.Count)
                {
                    m_spatialNavigationButtons[m_selectedButtonIndex].Select();
                }

                return;
            }

            if (m_selectedButtonIndex < m_toolkitNavigationButtons.Count)
            {
                m_toolkitNavigationButtons[m_selectedButtonIndex].Focus();
            }
        }

        private void ConfigureLoaderCollider()
        {
            m_panelCollider.center = Vector3.zero;
            m_panelCollider.size = new Vector3(k_panelWidthInPixels, k_panelHeightInPixels, 1f);
        }

        private void ConfigureBackButtonCollider()
        {
            float centerX = -k_panelWidthInPixels * 0.5f +
                            k_backButtonLeftInPixels +
                            k_backButtonWidthInPixels * 0.5f;
            float centerY = k_panelHeightInPixels * 0.5f -
                            k_backButtonTopInPixels -
                            k_backButtonHeightInPixels * 0.5f;
            m_panelCollider.center = new Vector3(centerX, centerY, 0f);
            m_panelCollider.size = new Vector3(
                k_backButtonWidthInPixels,
                k_backButtonHeightInPixels,
                1f);
        }

        private void PositionPanelInFrontOfCamera()
        {
            if (m_usesSpatialUi)
            {
                return;
            }

            if (m_mainCamera == null)
            {
                m_mainCamera = Camera.main;
            }

            if (m_mainCamera == null)
            {
                return;
            }

            Transform cameraTransform = m_mainCamera.transform;
            Vector3 panelPosition = cameraTransform.position +
                                    cameraTransform.forward * k_panelDistanceInMeters;
            transform.SetPositionAndRotation(panelPosition, cameraTransform.rotation);
        }

        private void LoadLoaderScene()
        {
            string loaderScenePath = GetLoaderScenePath(SceneManager.GetActiveScene());
            int loaderBuildIndex = FindBuildIndex(loaderScenePath);
            if (loaderBuildIndex < 0)
            {
                Debug.LogError($"Scene loader is missing from Build Settings: {loaderScenePath}");
                return;
            }

            LoadScene(loaderBuildIndex);
        }

        private async void LoadScene(int buildIndex)
        {
            if (m_isLoading)
            {
                return;
            }

            m_isLoading = true;
            if (m_usesSpatialUi)
            {
                m_spatialCanvas.gameObject.SetActive(false);
            }
            else
            {
                m_root.SetEnabled(false);
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!IsLoaderScene(activeScene))
            {
                int cleanupCount = await ShutdownActiveSpatialMLServicesAsync();
                Debug.Log(
                    $"{k_logTag} Drained {cleanupCount} SpatialML owner(s) in {activeScene.name}.");
            }

            SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
        }

        private async void PrepareForBackground(string lifecycleEvent)
        {
            if (m_isLoading ||
                m_shouldLoadLoaderAfterResume ||
                IsLoaderScene(SceneManager.GetActiveScene()))
            {
                return;
            }

            m_shouldLoadLoaderAfterResume = true;
            int cleanupCount = await ShutdownActiveSpatialMLServicesAsync();
            Debug.Log(
                $"{k_logTag} {lifecycleEvent} outside SceneLoader; " +
                $"shut down {cleanupCount} SpatialML owner(s).");

            if (!m_isApplicationPaused)
            {
                LoadLoaderAfterBackgroundIfNeeded();
            }
        }

        private void LoadLoaderAfterBackgroundIfNeeded()
        {
            if (!m_shouldLoadLoaderAfterResume)
            {
                return;
            }

            m_shouldLoadLoaderAfterResume = false;
            Debug.Log($"{k_logTag} Returned after sample backgrounding; requesting SceneLoader.");
            LoadLoaderScene();
        }

        private Task<int> ShutdownActiveSpatialMLServicesAsync()
        {
            m_activeCleanupTask ??= ShutdownActiveSpatialMLServicesCoreAsync();
            return m_activeCleanupTask;
        }

        private static Task<int> ShutdownActiveSpatialMLServicesCoreAsync()
        {
            MonoBehaviour[] sceneBehaviours =
                FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int cleanupCount = 0;

            for (int i = 0; i < sceneBehaviours.Length; i++)
            {
                if (sceneBehaviours[i] is ISpatialMLServiceCleanup cleanup)
                {
                    cleanup.ShutdownSpatialML();
                    cleanupCount++;
                }
            }

            return Task.FromResult(cleanupCount);
        }

        private static int FindBuildIndex(string scenePath)
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            for (int buildIndex = 0; buildIndex < sceneCount; buildIndex++)
            {
                if (SceneUtility.GetScenePathByBuildIndex(buildIndex) == scenePath)
                {
                    return buildIndex;
                }
            }

            return -1;
        }

        private static bool IsLoaderScene(Scene scene)
        {
            return IsLoaderScenePath(scene.path);
        }

        private static bool IsLoaderScenePath(string scenePath)
        {
            return scenePath == k_xrLoaderScenePath || scenePath == k_spatialLoaderScenePath;
        }

        private static bool IsSpatialLoaderScene(Scene scene)
        {
            return scene.path == k_spatialLoaderScenePath;
        }

        private static bool IsSpatialScenePath(string scenePath)
        {
            return scenePath.Replace('\\', '/').StartsWith(
                k_spatialSceneRoot,
                StringComparison.Ordinal);
        }

        private static string GetSceneDisplayName(string scenePath) =>
            scenePath == "Assets/Scenes/Spatial/FaceDetectionFullSpaceSpatial.unity"
                ? "Face Tracking (Full Space)" : Path.GetFileNameWithoutExtension(scenePath);

        private static string GetLoaderScenePath(Scene activeScene)
        {
            return IsSpatialScenePath(activeScene.path)
                ? k_spatialLoaderScenePath
                : k_xrLoaderScenePath;
        }
    }
}
