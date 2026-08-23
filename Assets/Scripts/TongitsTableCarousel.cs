using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]
public sealed class TongitsTableCarousel : MonoBehaviour,
    IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    [Header("Wiring")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private Button leftArrow;
    [SerializeField] private Button rightArrow;

    [Header("Selection")]
    [SerializeField, Min(0)] private int initialIndex = 0;
    [SerializeField] private bool loopNavigation = true;
    [SerializeField] private bool autoDiscoverDirectChildren = true;
    [SerializeField] private List<TongitsTableSelectable> items = new();

    [Header("Snap")]
    [SerializeField, Min(0.01f)] private float snapDuration = 0.28f;
    [SerializeField] private Ease snapEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledTime = true;
    [Header("Desktop Wheel")]
    [SerializeField, Min(0.01f)] private float wheelThreshold = 0.25f;
    [SerializeField, Min(0f)] private float wheelCooldown = 0.12f;

    [Header("Events")]
    [SerializeField] private UnityEvent<int> onSelectionChanged = new();

    private int selectedIndex = -1;
    private Tween snapTween;
    private float lastWheelTime = -100f;

    public int SelectedIndex => selectedIndex;
    public IReadOnlyList<TongitsTableSelectable> Items => items;

    private void Awake()
    {
        EnsureReferences();
        EnsureArrowButtons();
        ConfigureScrollRect();
        RebuildItems();
    }

    private void OnEnable()
    {
        if (leftArrow != null) leftArrow.onClick.AddListener(SelectPrevious);
        if (rightArrow != null) rightArrow.onClick.AddListener(SelectNext);
    }

    private void OnDisable()
    {
        if (leftArrow != null) leftArrow.onClick.RemoveListener(SelectPrevious);
        if (rightArrow != null) rightArrow.onClick.RemoveListener(SelectNext);
        snapTween?.Kill();
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        ForceLayoutNow();
        int safeInitial = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, items.Count - 1));
        Select(safeInitial, true, false);

        if (EventSystem.current != null && items.Count > 0 && items[safeInitial] != null)
            EventSystem.current.SetSelectedGameObject(items[safeInitial].gameObject);
    }

    public void RebuildItems()
    {
        EnsureReferences();
        if (autoDiscoverDirectChildren)
        {
            items.Clear();
            if (content != null)
            {
                for (int i = 0; i < content.childCount; i++)
                {
                    Transform child = content.GetChild(i);
                    TongitsTableSelectable item = child.GetComponent<TongitsTableSelectable>();
                    if (item == null) item = child.gameObject.AddComponent<TongitsTableSelectable>();
                    items.Add(item);
                }
            }
        }

        for (int i = 0; i < items.Count; i++)
            if (items[i] != null) items[i].Configure(this, i);
    }
    public void SelectPrevious()
    {
        Select(selectedIndex < 0 ? 0 : selectedIndex - 1);
    }

    public void SelectNext()
    {
        Select(selectedIndex < 0 ? 0 : selectedIndex + 1);
    }

    public void Select(int index)
    {
        Select(index, false, true);
    }

    public void Select(int index, bool instant, bool invokeEvent)
    {
        if (items == null || items.Count == 0) return;

        int nextIndex = NormalizeIndex(index);
        bool changed = selectedIndex != nextIndex;
        selectedIndex = nextIndex;

        for (int i = 0; i < items.Count; i++)
            if (items[i] != null) items[i].SetSelected(i == selectedIndex, instant);

        SnapTo(selectedIndex, instant);
        UpdateArrowState();

        if (changed && invokeEvent)
            onSelectionChanged?.Invoke(selectedIndex);
    }
    private int NormalizeIndex(int index)
    {
        if (!loopNavigation)
            return Mathf.Clamp(index, 0, items.Count - 1);

        if (index < 0) return items.Count - 1;
        if (index >= items.Count) return 0;
        return index;
    }

    private void SnapTo(int index, bool instant)
    {
        if (content == null || viewport == null || index < 0 || index >= items.Count)
            return;

        ForceLayoutNow();
        scrollRect?.StopMovement();
        snapTween?.Kill();

        RectTransform itemRect = items[index].RectTransform;
        Vector3 itemWorldCenter = itemRect.TransformPoint(itemRect.rect.center);
        float itemCenterX = viewport.InverseTransformPoint(itemWorldCenter).x;
        float viewportCenterX = viewport.rect.center.x;
        float targetX = content.anchoredPosition.x + (viewportCenterX - itemCenterX);

        Vector2 target = content.anchoredPosition;
        target.x = targetX;

        if (instant || !Application.isPlaying)
        {
            content.anchoredPosition = target;
            return;
        }
        snapTween = content.DOAnchorPos(target, snapDuration)
            .SetEase(snapEase)
            .SetUpdate(useUnscaledTime)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void ForceLayoutNow()
    {
        if (content == null || viewport == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        if (layout != null && content.childCount > 0)
        {
            RectTransform first = content.GetChild(0) as RectTransform;
            if (first != null && viewport.rect.width > 0f)
            {
                int sidePadding = Mathf.Max(0,
                    Mathf.RoundToInt((viewport.rect.width - first.rect.width) * 0.5f));
                if (layout.padding.left != sidePadding || layout.padding.right != sidePadding)
                {
                    layout.padding.left = sidePadding;
                    layout.padding.right = sidePadding;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                }
            }
        }
    }

    private void EnsureReferences()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
        if (viewport == null && scrollRect != null) viewport = scrollRect.viewport;
        if (content == null && scrollRect != null) content = scrollRect.content;
    }

    private void EnsureArrowButtons()
    {
        leftArrow = EnsureArrowButton(leftArrow, "ArrowLeft", false);
        rightArrow = EnsureArrowButton(rightArrow, "ArrowRight", true);
    }

    private static Button EnsureArrowButton(Button current, string objectName, bool mirrorRight)
    {
        GameObject go = current != null ? current.gameObject : GameObject.Find(objectName);
        if (go == null) return current;

        go.transform.SetAsLastSibling();
        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect != null && mirrorRight)
        {
            Vector3 scale = rect.localScale;
            rect.localRotation = Quaternion.identity;
            scale.x = -Mathf.Abs(scale.x);
            rect.localScale = scale;
        }

        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        Graphic graphic = go.GetComponent<Graphic>();
        if (graphic != null) button.targetGraphic = graphic;
        return button;
    }

    private void ConfigureScrollRect()
    {
        if (scrollRect == null) return;

        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.inertia = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 0f;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        snapTween?.Kill();
        scrollRect?.StopMovement();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ForceLayoutNow();
        int nearest = FindClosestIndex();
        if (nearest >= 0) Select(nearest);
    }

    public void OnScroll(PointerEventData eventData)
    {
        float delta = Mathf.Abs(eventData.scrollDelta.y) >= Mathf.Abs(eventData.scrollDelta.x)
            ? eventData.scrollDelta.y
            : -eventData.scrollDelta.x;

        if (Mathf.Abs(delta) < wheelThreshold) return;
        if (Time.unscaledTime - lastWheelTime < wheelCooldown) return;

        lastWheelTime = Time.unscaledTime;
        if (delta < 0f) SelectNext();
        else SelectPrevious();
    }
    private int FindClosestIndex()
    {
        if (items == null || items.Count == 0 || viewport == null) return -1;

        float centerX = viewport.rect.center.x;
        float bestDistance = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            RectTransform rect = items[i].RectTransform;
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            float localX = viewport.InverseTransformPoint(worldCenter).x;
            float distance = Mathf.Abs(localX - centerX);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private void UpdateArrowState()
    {
        bool hasMultiple = items != null && items.Count > 1;
        if (leftArrow != null)
            leftArrow.interactable = hasMultiple && (loopNavigation || selectedIndex > 0);
        if (rightArrow != null)
            rightArrow.interactable = hasMultiple &&
                (loopNavigation || selectedIndex < items.Count - 1);
    }
}