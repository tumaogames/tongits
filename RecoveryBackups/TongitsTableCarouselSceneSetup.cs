#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TongitsTableCarouselSceneSetup
{
    private const string ScenePath = "Assets/Scenes/MainScene1.unity";
    private const string ScrollViewName = "TableScrollView";
    private const string ViewportName = "Viewport";

    [InitializeOnLoadMethod]
    private static void AutoSetupOnReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded) return;
            if (GameObject.Find("TableContainer") == null ||
                GameObject.Find("ArrowLeft") == null ||
                GameObject.Find("ArrowRight") == null) return;
            if (Object.FindFirstObjectByType<TongitsTableCarousel>() != null) return;
            SetupActiveScene(false);
        };
    }

    [MenuItem("Tools/Tongits/Build Professional Table Carousel")]
    public static void SetupFromMenu()
    {
        SetupActiveScene(false);
    }

    public static void SetupMainSceneBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SetupActiveScene(true);
    }

    private static void SetupActiveScene(bool saveScene)
    {
        GameObject tableContainer = GameObject.Find("TableContainer");
        GameObject arrowLeft = GameObject.Find("ArrowLeft");
        GameObject arrowRight = GameObject.Find("ArrowRight");

        if (tableContainer == null || arrowLeft == null || arrowRight == null)
        {
            Debug.LogError("[Tongits Carousel] Missing TableContainer, ArrowLeft, or ArrowRight.");
            return;
        }

        RectTransform contentRect = tableContainer.GetComponent<RectTransform>();
        RectTransform originalParent = contentRect.parent as RectTransform;
        if (originalParent == null)
        {
            Debug.LogError("[Tongits Carousel] TableContainer requires a RectTransform parent.");
            return;
        }

        GameObject scrollView = FindDirectChild(originalParent, ScrollViewName);
        if (scrollView == null)
            scrollView = CreateScrollViewShell(contentRect, originalParent);

        RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
        GameObject viewportObject = FindDirectChild(scrollRectTransform, ViewportName);
        if (viewportObject == null)
            viewportObject = CreateViewport(scrollRectTransform);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        if (contentRect.parent != viewportRect)
        {
            Undo.SetTransformParent(contentRect, viewportRect, "Move TableContainer into carousel viewport");
            contentRect.anchorMin = new Vector2(0f, 0.5f);
            contentRect.anchorMax = new Vector2(0f, 0.5f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.localRotation = Quaternion.identity;
            contentRect.localScale = Vector3.one;
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, scrollRectTransform.rect.height);
        }

        ConfigureContent(tableContainer);
        ScrollRect uiScrollRect = GetOrAdd<ScrollRect>(scrollView);
        uiScrollRect.content = contentRect;
        uiScrollRect.viewport = viewportRect;
        uiScrollRect.horizontal = true;
        uiScrollRect.vertical = false;
        uiScrollRect.movementType = ScrollRect.MovementType.Clamped;
        uiScrollRect.inertia = false;
        uiScrollRect.scrollSensitivity = 0f;
        uiScrollRect.horizontalScrollbar = null;
        uiScrollRect.verticalScrollbar = null;

        Button leftButton = ConfigureArrow(arrowLeft);
        Button rightButton = ConfigureArrow(arrowRight);
        ConfigureTableItems(contentRect);
        TongitsTableCarousel carousel = GetOrAdd<TongitsTableCarousel>(scrollView);
        SerializedObject serializedCarousel = new SerializedObject(carousel);
        serializedCarousel.FindProperty("scrollRect").objectReferenceValue = uiScrollRect;
        serializedCarousel.FindProperty("viewport").objectReferenceValue = viewportRect;
        serializedCarousel.FindProperty("content").objectReferenceValue = contentRect;
        serializedCarousel.FindProperty("leftArrow").objectReferenceValue = leftButton;
        serializedCarousel.FindProperty("rightArrow").objectReferenceValue = rightButton;
        serializedCarousel.FindProperty("initialIndex").intValue = Mathf.Clamp(1, 0, Mathf.Max(0, contentRect.childCount - 1));
        serializedCarousel.FindProperty("loopNavigation").boolValue = true;
        serializedCarousel.FindProperty("autoDiscoverDirectChildren").boolValue = true;
        serializedCarousel.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(scrollView);
        EditorUtility.SetDirty(tableContainer);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (saveScene)
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log($"[Tongits Carousel] Ready: {contentRect.childCount} selectable tables, arrows wired, swipe + DOTween snap enabled.");
    }

    private static GameObject CreateScrollViewShell(RectTransform source, RectTransform parent)
    {
        int siblingIndex = source.GetSiblingIndex();
        GameObject go = new GameObject(ScrollViewName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Tongits TableScrollView");
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.SetSiblingIndex(siblingIndex);
        CopyRectTransform(source, rect);
        return go;
    }
    private static GameObject CreateViewport(RectTransform parent)
    {
        GameObject go = new GameObject(ViewportName, typeof(RectTransform), typeof(Image), typeof(Mask));
        Undo.RegisterCreatedObjectUndo(go, "Create Tongits carousel viewport");
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;
        Mask mask = go.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        return go;
    }

    private static void ConfigureContent(GameObject tableContainer)
    {
        HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(tableContainer);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;
        if (layout.spacing < 24f) layout.spacing = 66f;

        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(tableContainer);
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        Image image = tableContainer.GetComponent<Image>();
        if (image != null) image.raycastTarget = false;
    }

    private static void ConfigureTableItems(RectTransform content)
    {
        for (int i = 0; i < content.childCount; i++)
        {
            GameObject itemObject = content.GetChild(i).gameObject;
            CanvasGroup group = GetOrAdd<CanvasGroup>(itemObject);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            Button button = GetOrAdd<Button>(itemObject);
            button.targetGraphic = itemObject.GetComponent<Graphic>();
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            GetOrAdd<TongitsTableSelectable>(itemObject);
            EditorUtility.SetDirty(itemObject);
        }
    }

    private static Button ConfigureArrow(GameObject arrow)
    {
        arrow.transform.SetAsLastSibling();
        RectTransform rect = arrow.GetComponent<RectTransform>();
        if (rect != null && arrow.name == "ArrowRight")
        {
            Vector3 scale = rect.localScale;
            rect.localRotation = Quaternion.identity;
            scale.x = -Mathf.Abs(scale.x);
            rect.localScale = scale;
        }

        Button button = GetOrAdd<Button>(arrow);
        button.targetGraphic = arrow.GetComponent<Graphic>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.82f, 0.88f, 1f, 1f);
        colors.selectedColor = new Color(0.92f, 0.96f, 1f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        EditorUtility.SetDirty(arrow);
        return button;
    }

    private static GameObject FindDirectChild(RectTransform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child.gameObject;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }
    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }
}
#endif
