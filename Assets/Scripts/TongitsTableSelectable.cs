using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class TongitsTableSelectable : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Visual Root")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Scale")]
    [SerializeField, Range(0.6f, 1.2f)] private float idleScale = 0.92f;
    [SerializeField, Range(0.8f, 1.3f)] private float hoverScale = 1.00f;
    [SerializeField, Range(0.8f, 1.4f)] private float selectedScale = 1.10f;
    [SerializeField, Range(0.7f, 1.2f)] private float pressedScale = 0.96f;

    [Header("Opacity")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.68f;
    [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.88f;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float tweenDuration = 0.18f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;
    [SerializeField] private bool selectOnEventSystemFocus = true;

    private TongitsTableCarousel owner;
    private Button button;
    private int index = -1;
    private bool selected;
    private bool hovered;
    private bool pressed;
    private bool eventFocused;
    private Vector3 baseScale = Vector3.one;

    public RectTransform RectTransform => (RectTransform)transform;
    public int Index => index;
    public bool IsSelected => selected;

    private void Awake()
    {
        EnsureReferences();
        baseScale = visualRoot.localScale;
    }

    private void OnEnable()
    {
        EnsureReferences();
        button.onClick.AddListener(HandleClick);
    }
    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);

        DOTween.Kill(this);
    }

    public void Configure(TongitsTableCarousel carousel, int itemIndex)
    {
        owner = carousel;
        index = itemIndex;
        EnsureReferences();

        if (baseScale == Vector3.zero)
            baseScale = visualRoot.localScale;
    }

    public void SetSelected(bool value, bool instant = false)
    {
        selected = value;
        RefreshVisual(instant);
    }

    public void SnapVisualImmediate()
    {
        RefreshVisual(true);
    }

    private void EnsureReferences()
    {
        if (visualRoot == null) visualRoot = (RectTransform)transform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (button == null) button = GetComponent<Button>();
    }
    private void HandleClick()
    {
        owner?.Select(index);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RefreshVisual(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        RefreshVisual(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        RefreshVisual(false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        RefreshVisual(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        eventFocused = true;
        if (selectOnEventSystemFocus && owner != null && owner.SelectedIndex != index)
            owner.Select(index);
        else
            RefreshVisual(false);
    }
    public void OnDeselect(BaseEventData eventData)
    {
        eventFocused = false;
        RefreshVisual(false);
    }

    private void RefreshVisual(bool instant)
    {
        EnsureReferences();
        DOTween.Kill(this);

        float scaleFactor = selected
            ? selectedScale
            : pressed ? pressedScale
            : (hovered || eventFocused) ? hoverScale
            : idleScale;

        float alpha = selected
            ? selectedAlpha
            : (hovered || eventFocused) ? hoverAlpha
            : idleAlpha;

        Vector3 targetScale = Vector3.Scale(baseScale, Vector3.one * scaleFactor);
        if (instant || !Application.isPlaying)
        {
            visualRoot.localScale = targetScale;
            canvasGroup.alpha = alpha;
            return;
        }

        visualRoot.DOScale(targetScale, tweenDuration)
            .SetEase(scaleEase).SetUpdate(true).SetId(this);
        canvasGroup.DOFade(alpha, tweenDuration)
            .SetEase(fadeEase).SetUpdate(true).SetId(this);
    }
}