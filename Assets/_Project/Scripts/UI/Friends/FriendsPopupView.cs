using System;
using System.Collections.Generic;
using TMPro;
using TransportManager.Core;
using TransportManager.Events;
using TransportManager.Social;
using TransportManager.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace TransportManager.UI.Friends
{
    public class FriendsPopupView : MonoBehaviour
    {
        private static readonly Color32 BgOverlay  = new Color32(0x00, 0x00, 0x00, 180);
        private static readonly Color32 BgPanel    = new Color32(0x16, 0x19, 0x1F, 255);
        private static readonly Color32 BgCard     = new Color32(0x1F, 0x23, 0x2B, 255);
        private static readonly Color32 BgPill     = new Color32(0x2C, 0x32, 0x3C, 255);
        private static readonly Color32 Accent     = new Color32(0x3D, 0xC9, 0x6E, 255);
        private static readonly Color32 AccentBlue = new Color32(0x35, 0x8E, 0xF5, 255);
        private static readonly Color32 TextPri    = new Color32(0xEC, 0xEF, 0xF5, 255);
        private static readonly Color32 TextSec    = new Color32(0x9A, 0xA5, 0xB8, 255);
        private static readonly Color32 TextMuted  = new Color32(0x55, 0x63, 0x78, 255);
        private static readonly Color32 Divider    = new Color32(0x28, 0x2D, 0x38, 255);

        private Sprite    _sprRound12;
        private Sprite    _sprRound8;
        private Sprite    _sprPill;
        private Transform _listContent;
        private readonly System.Collections.Generic.Dictionary<string, bool> _routeVisible =
            new System.Collections.Generic.Dictionary<string, bool>();

        private const int TitleBarH    = 56;
        private const int InviteAreaH  = 76; // 12 + 52 + 12

        // ── Entry point ───────────────────────────────────────────────────────

        public static void Show()
        {
            if (FindObjectOfType<FriendsPopupView>() != null) return;
            new GameObject("FriendsPopup", typeof(RectTransform)).AddComponent<FriendsPopupView>();
        }

        private void Awake()
        {
            _sprRound12 = MakeRoundedSprite(12);
            _sprRound8  = MakeRoundedSprite(8);
            _sprPill    = MakeRoundedSprite(32);
            BuildUI();
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var overlay = MakeImg("Overlay", transform, BgOverlay);
            overlay.raycastTarget = true;
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            FillParent(overlay.GetComponent<RectTransform>());

            var panelGo  = MakeGO("Panel", transform);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.sprite        = _sprRound12;
            panelImg.type          = Image.Type.Sliced;
            panelImg.color         = BgPanel;
            panelImg.raycastTarget = true;
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.05f, 0.06f);
            panelRt.anchorMax = new Vector2(0.95f, 0.94f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            PopupHeader.Build(panelGo.transform, "Amis", Close, TitleBarH, _sprRound8);
            BuildInviteBar(panelGo.transform);
            BuildScrollArea(panelGo.transform);
        }

        // ── Invite bar (fixed, below title) ───────────────────────────────────

        private void BuildInviteBar(Transform parent)
        {
            int top    = TitleBarH + 1;
            int bottom = top + InviteAreaH;

            var bar   = MakeGO("InviteBar", parent);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot     = new Vector2(0.5f, 1);
            barRt.offsetMin = new Vector2(14, -bottom);
            barRt.offsetMax = new Vector2(-14, -top);

            var btn    = MakeGO("BtnInvite", bar.transform);
            var btnImg = btn.AddComponent<Image>();
            btnImg.sprite = _sprRound8;
            btnImg.type   = Image.Type.Sliced;
            btnImg.color  = Accent;
            FillParent(btn.GetComponent<RectTransform>());
            var btnComp = btn.AddComponent<Button>();
            btnComp.targetGraphic = btnImg;
            btnComp.onClick.AddListener(OnInviteFriend);

            // Icon + label row
            var row    = MakeGO("Row", btn.transform);
            var rowRt  = row.GetComponent<RectTransform>();
            FillParent(rowRt);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.spacing               = 10;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            var iconGo  = MakeGO("Icon", row.transform);
            var iconImg = iconGo.AddComponent<Image>();
            var shareSprite = Resources.Load<Sprite>("UI/Icons/icons/share");
            iconImg.sprite         = shareSprite != null ? shareSprite : Resources.Load<Sprite>("UI/Icons/icons/users");
            iconImg.color          = TextPri;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(22, 22);

            var lbl = AddTMP("Lbl", row.transform, "Inviter un ami", 16, FontStyles.Bold, TextPri);
            lbl.raycastTarget = false;
        }

        // ── Scroll area (friends list) ────────────────────────────────────────

        private void BuildScrollArea(Transform parent)
        {
            int scrollTopOffset = TitleBarH + 1 + InviteAreaH;

            var scrollGo = MakeGO("Scroll", parent);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(0, 0);
            scrollRt.offsetMax = new Vector2(0, -scrollTopOffset);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal        = false;
            scrollRect.vertical          = true;
            scrollRect.scrollSensitivity = 50;
            scrollRect.movementType      = ScrollRect.MovementType.Elastic;

            var viewport = MakeGO("Viewport", scrollGo.transform);
            FillParent(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            var content   = MakeGO("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 14, 10, 16);
            vlg.spacing               = 8;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            _listContent = content.transform;

            var sectionLbl = AddTMP("LblSection", _listContent, "MES AMIS", 10, FontStyles.Bold, TextMuted);
            sectionLbl.characterSpacing = 120;
            sectionLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            var friends = FriendsData.LoadAll();
#if UNITY_EDITOR
            if (friends.Count == 0)
            {
                friends.Add(new FriendEntry { uid = "test_001", companyName = "Dupont Transport",  level = 7,  vehicleCount = 4 });
                friends.Add(new FriendEntry { uid = "test_002", companyName = "Martin Logistics",  level = 12, vehicleCount = 9 });
                friends.Add(new FriendEntry { uid = "test_003", companyName = "Express du Sud",    level = 3,  vehicleCount = 1 });
            }
#endif
            if (friends.Count == 0)
                BuildEmptyState();
            else
                foreach (var f in friends)
                    BuildFriendCard(f);
        }

        // ── Friend card ───────────────────────────────────────────────────────

        private void BuildEmptyState()
        {
            var go = MakeGO("Empty", _listContent);
            go.AddComponent<LayoutElement>().preferredHeight = 160;

            var lbl = AddTMP("Lbl", go.transform,
                             "Aucun ami pour l'instant.\nInvite tes amis pour voir\nleurs entreprises !",
                             14, FontStyles.Normal, TextMuted);
            lbl.alignment        = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.Normal;
            FillParent(lbl.GetComponent<RectTransform>());
        }

        private void BuildFriendCard(FriendEntry friend)
        {
            var card    = MakeGO("Card_" + friend.uid, _listContent);
            var cardImg = card.AddComponent<Image>();
            cardImg.sprite        = _sprRound12;
            cardImg.type          = Image.Type.Sliced;
            cardImg.color         = BgCard;
            cardImg.raycastTarget = false;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(14, 14, 12, 12);
            vlg.spacing               = 10;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;

            // ── Info row ──
            var infoRow = MakeGO("InfoRow", card.transform);
            infoRow.AddComponent<LayoutElement>().preferredHeight = 44;
            var infoHlg = infoRow.AddComponent<HorizontalLayoutGroup>();
            infoHlg.childAlignment        = TextAnchor.MiddleLeft;
            infoHlg.spacing               = 10;
            infoHlg.childForceExpandWidth  = false;
            infoHlg.childForceExpandHeight = true;
            infoHlg.childControlWidth      = false;
            infoHlg.childControlHeight     = true;

            // Avatar circle with initial
            var avatarGo  = MakeGO("Avatar", infoRow.transform);
            var avatarImg = avatarGo.AddComponent<Image>();
            avatarImg.sprite = _sprPill;
            avatarImg.type   = Image.Type.Sliced;
            avatarImg.color  = new Color32(0x35, 0x8E, 0xF5, 50);
            var avatarLe = avatarGo.AddComponent<LayoutElement>();
            avatarLe.preferredWidth  = 40;
            avatarLe.preferredHeight = 40;

            string initial = friend.companyName.Length > 0
                ? friend.companyName[0].ToString().ToUpper()
                : "?";
            var initLbl = AddTMP("Initial", avatarGo.transform, initial, 18, FontStyles.Bold, AccentBlue);
            initLbl.alignment = TextAlignmentOptions.Center;
            FillParent(initLbl.GetComponent<RectTransform>());

            // Name + level column
            var infoCol = MakeGO("InfoCol", infoRow.transform);
            infoCol.AddComponent<LayoutElement>().flexibleWidth = 1;
            var infoColVlg = infoCol.AddComponent<VerticalLayoutGroup>();
            infoColVlg.childAlignment        = TextAnchor.MiddleLeft;
            infoColVlg.childForceExpandWidth  = true;
            infoColVlg.childForceExpandHeight = false;
            infoColVlg.childControlWidth      = true;
            infoColVlg.childControlHeight     = true;

            var nameLbl = AddTMP("Name", infoCol.transform, friend.companyName, 15, FontStyles.Bold, TextPri);
            nameLbl.textWrappingMode = TextWrappingModes.NoWrap;
            nameLbl.overflowMode     = TextOverflowModes.Ellipsis;
            nameLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            string truckLabel = friend.vehicleCount > 1 ? "camions" : "camion";
            var levelLbl = AddTMP("Level", infoCol.transform,
                                   $"Niveau {friend.level} · {friend.vehicleCount} {truckLabel}",
                                   12, FontStyles.Normal, TextSec);
            levelLbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            // ── Divider ──
            var div    = MakeGO("Div", card.transform);
            var divImg = div.AddComponent<Image>();
            divImg.color         = Divider;
            divImg.raycastTarget = false;
            div.AddComponent<LayoutElement>().preferredHeight = 1;

            // ── Action buttons row ──
            var actRow = MakeGO("ActRow", card.transform);
            actRow.AddComponent<LayoutElement>().preferredHeight = 36;
            var actHlg = actRow.AddComponent<HorizontalLayoutGroup>();
            actHlg.spacing               = 6;
            actHlg.childAlignment        = TextAnchor.MiddleCenter;
            actHlg.childForceExpandWidth  = true;
            actHlg.childForceExpandHeight = true;
            actHlg.childControlWidth      = true;
            actHlg.childControlHeight     = true;

            string uid = friend.uid;
            AddActionBtn(actRow.transform, "Dépôt",  "warehouse", () => OnViewDepot(uid));
            AddActionBtn(actRow.transform, "Camions", "truck", () => OnViewTrucks(uid));
            AddToggleRouteBtn(actRow.transform, uid);
        }

        private void AddToggleRouteBtn(Transform parent, string uid)
        {
            var go  = MakeGO("Btn_Trajets", parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = BgPill;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.spacing               = 5;
            hlg.padding               = new RectOffset(6, 6, 4, 4);
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            var iconGo  = MakeGO("Icon", go.transform);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 14);

            var lbl = AddTMP("Lbl", go.transform, "Trajets", 12, FontStyles.Normal, TextSec);
            lbl.raycastTarget = false;

            bool isOn = _routeVisible.TryGetValue(uid, out bool v) && v;
            RefreshRouteBtn(iconImg, lbl, isOn);

            btn.onClick.AddListener(() =>
            {
                bool nowOn = !(_routeVisible.TryGetValue(uid, out bool cur) && cur);
                _routeVisible[uid] = nowOn;
                RefreshRouteBtn(iconImg, lbl, nowOn);
                if (nowOn) GameEvents.RaiseFriendRoutesRequested(uid);
                else       GameEvents.RaiseFriendRoutesHidden(uid);
            });
        }

        private static void RefreshRouteBtn(Image iconImg, TMP_Text lbl, bool isOn)
        {
            iconImg.sprite = Resources.Load<Sprite>(isOn ? "UI/Icons/icons/eye" : "UI/Icons/icons/eye-off");
            iconImg.color  = isOn ? new Color32(0x3D, 0xC9, 0x6E, 255) : new Color32(0x9A, 0xA5, 0xB8, 255);
            lbl.text       = isOn ? "Masquer" : "Trajets";
            lbl.color      = isOn ? new Color32(0x3D, 0xC9, 0x6E, 255) : new Color32(0x9A, 0xA5, 0xB8, 255);
        }

        private void AddActionBtn(Transform parent, string label, string iconName, Action onClick)
        {
            var go  = MakeGO("Btn_" + label, parent);
            var img = go.AddComponent<Image>();
            img.sprite = _sprRound8;
            img.type   = Image.Type.Sliced;
            img.color  = BgPill;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.spacing               = 5;
            hlg.padding               = new RectOffset(6, 6, 4, 4);
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            var iconGo  = MakeGO("Icon", go.transform);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite         = Resources.Load<Sprite>($"UI/Icons/icons/{iconName}");
            iconImg.color          = TextSec;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 14);

            var lbl = AddTMP("Lbl", go.transform, label, 12, FontStyles.Normal, TextSec);
            lbl.raycastTarget = false;
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void OnInviteFriend()
        {
            var gm = GameManager.Instance;
            string companyName = gm?.Save?.company?.companyName ?? "Mon Entreprise";
            string link        = FriendsData.GenerateInviteLink();
            string message     = $"{companyName} t'invite à rejoindre Transport Manager ! {link}";
            NativeShare.ShareText(message);
        }

        private void OnViewDepot(string uid)
        {
            Debug.Log($"[Friends] Voir dépôt de {uid}");
            GameEvents.RaiseFriendDepotRequested(uid);
            Close();
        }

        private void OnViewTrucks(string uid)
        {
            Debug.Log($"[Friends] Voir camions de {uid}");
            GameEvents.RaiseFriendTrucksRequested(uid);
            Close();
        }

        private void Close() => Destroy(gameObject);

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image MakeImg(string name, Transform parent, Color32 color)
        {
            var go  = MakeGO(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text AddTMP(string name, Transform parent, string text,
                                       float size, FontStyles style, Color32 color)
        {
            var go  = MakeGO(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.fontStyle     = style;
            tmp.color         = color;
            tmp.alignment     = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void FillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeRoundedSprite(int radius)
        {
            const int size = 64;
            int r   = Mathf.Clamp(radius, 1, size / 2);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, RoundedAlpha(x, y, size, r));
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                                 new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private static float RoundedAlpha(int x, int y, int size, int r)
        {
            int cx = -1, cy = -1;
            if      (x < r         && y < r)          { cx = r;        cy = r;        }
            else if (x >= size - r && y < r)          { cx = size - r; cy = r;        }
            else if (x < r         && y >= size - r)  { cx = r;        cy = size - r; }
            else if (x >= size - r && y >= size - r)  { cx = size - r; cy = size - r; }
            if (cx < 0) return 1f;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }
    }
}
