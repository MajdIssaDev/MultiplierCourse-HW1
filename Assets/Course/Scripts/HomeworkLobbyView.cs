using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// A minimal homework menu. Only Photon/model events refresh the displayed data.
public sealed class HomeworkLobbyView : MonoBehaviour
{
    private LobbyManager model;
    private Font font;
    private Transform root, roomRows;
    private GameObject lobbyPanel, roomPanel;
    private Text status, context, roomTitle, count, players, empty;
    private InputField lobbyInput, roomInput, capacityInput, nameInput;
    private Button connect, leave, create, join;

    public void Bind(LobbyManager manager)
    {
        model = manager;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = new GameObject("Lobby UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scale = canvas.GetComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1000,690);
        scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root = canvas.transform;
        canvas.AddComponent<CanvasTextRefresher>();
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        Box(root,"Background",0,0,1000,690,new Color(.76f,.76f,.76f));
        Label(root,"Lobby",20,18,500,30,22,true);
        context = Label(root,"",20,54,950,27,16);
        Label(root,"Lobby name",20,96,105,26,16);
        lobbyInput = Input(root,model.LobbyName,125,90,280,32);
        connect = Button(root,"Join lobby",420,90,130,32,()=>model.JoinLobby(lobbyInput.text));
        leave = Button(root,"Leave lobby",565,90,170,32,()=>model.LeaveOrCancel());

        Label(root,"Your name",20,146,105,26,16);
        nameInput = Input(root,model.PlayerName,125,140,280,32);
        nameInput.characterLimit = 24;
        nameInput.onValueChanged.AddListener(model.SetPlayerName);

        lobbyPanel = Rect(root,"Lobby controls",20,200,950,270).gameObject;
        Label(lobbyPanel.transform,"Room name",0,5,105,26,16);
        roomInput = Input(lobbyPanel.transform,model.DefaultRoom,110,0,240,32);
        Label(lobbyPanel.transform,"Max players",370,5,100,26,16);
        capacityInput = Input(lobbyPanel.transform,"2",475,0,50,32);
        capacityInput.contentType = InputField.ContentType.IntegerNumber;
        create = Button(lobbyPanel.transform,"Create room",540,0,130,32,()=> {
            int.TryParse(capacityInput.text,out int n);
            model.EnterRoom(roomInput.text,n,true);
        });
        join = Button(lobbyPanel.transform,"Join by name",685,0,135,32,()=>model.EnterRoom(roomInput.text,2,false));
        Label(lobbyPanel.transform,"Available rooms (click to join)",0,56,620,26,18);
        empty = Label(lobbyPanel.transform,"",0,88,620,26,16);
        var scrollGo = Box(lobbyPanel.transform,"Rooms",0,119,620,145,new Color(.82f,.82f,.82f));
        scrollGo.AddComponent<RectMask2D>();
        var scroll = scrollGo.AddComponent<ScrollRect>();
        var content = new GameObject("Sessions",typeof(RectTransform));
        content.transform.SetParent(scrollGo.transform,false);
        roomRows = content.transform;
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0,1);
        contentRect.anchorMax = new Vector2(1,1);
        contentRect.pivot = new Vector2(0,1);
        contentRect.sizeDelta = new Vector2(0,145);
        scroll.content = contentRect;
        scroll.viewport = scrollGo.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        roomPanel = Rect(root,"Room controls",20,200,950,270).gameObject;
        roomTitle = Label(roomPanel.transform,"",0,0,930,30,20);
        count = Label(roomPanel.transform,"",0,42,930,28,18);
        players = Label(roomPanel.transform,"",0,83,930,175,18);
        status = Label(root,"",20,500,950,70,16);
        model.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        bool offline = model.State == LobbyManager.LobbyState.Offline;
        bool lobby = model.State == LobbyManager.LobbyState.InLobby;
        bool room = model.State == LobbyManager.LobbyState.InRoom;
        bool leaving = model.State == LobbyManager.LobbyState.Leaving;
        context.text = model.ClientLabel+" | Lobby: "+model.LobbyName+" | "+model.Region+" | "+model.State;
        Paint(lobbyInput, offline);
        Paint(nameInput, offline || lobby);
        Paint(connect, offline);
        leave.gameObject.SetActive(!offline);
        Paint(leave, !leaving);
        leave.GetComponentInChildren<Text>().text = room ? "Leave room" : lobby ? "Leave lobby" : leaving ? "Leaving..." : "Cancel";
        lobbyPanel.SetActive(!room);
        roomPanel.SetActive(room);
        Paint(create, lobby);
        Paint(join, lobby);
        Paint(roomInput, lobby);
        Paint(capacityInput, lobby);
        status.text = "Status: "+model.Status;
        roomTitle.text = "Room: "+model.RoomName;
        count.text = "Players: "+model.Players.Count+" / "+model.MaxPlayers;
        var entries = model.Players.Select(p=>model.DisplayName(p)+" ["+p+"]"+(p == model.LocalPlayerId ? " (you)" : "")).ToArray();
        players.text = string.Join("\n",Enumerable.Range(0,Math.Min(5,entries.Length)).Select(i=>
            entries[i]+(i+5 < entries.Length ? "                    "+entries[i+5] : "")));

        foreach (Transform child in roomRows) Destroy(child.gameObject);
        roomRows.GetComponent<RectTransform>().sizeDelta = new Vector2(0,Mathf.Max(145,model.Sessions.Count*38));
        empty.text = !lobby ? "Join a lobby first." : model.Sessions.Count == 0 ? "No rooms." : model.Sessions.Count+" room(s)";
        int row = 0;
        foreach (var session in model.Sessions)
        {
            string name = session.Name;
            bool available = session.IsOpen && session.PlayerCount < session.MaxPlayers;
            string who = LobbyManager.RoomPlayerNames(session);
            var button = Button(roomRows,name+"   "+session.PlayerCount+"/"+session.MaxPlayers+
                (who.Length > 0 ? "   ("+who+")" : "")+(available ? "   Join" : "   Full / closed"),
                0,row++*38,610,32,()=>model.EnterRoom(name,session.MaxPlayers,false));
            Paint(button, lobby && available);
        }
    }

    // Enabled controls: light background + black text. Disabled: grey background + dark grey text.
    // Colors are set directly (transition = None) so text always stays readable.
    private static void Paint(Selectable control, bool enabled)
    {
        control.interactable = enabled;
        if (control.targetGraphic != null)
            control.targetGraphic.color = enabled ? (control is InputField ? Color.white : new Color(.93f,.93f,.93f)) : new Color(.72f,.72f,.72f);
        foreach (var text in control.GetComponentsInChildren<Text>(true))
            text.color = enabled ? Color.black : new Color(.25f,.25f,.25f);
    }

    private RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go = new GameObject(name,typeof(RectTransform));
        go.transform.SetParent(parent,false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0,1);
        rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y);
        rect.sizeDelta = new Vector2(w,h);
        return rect;
    }
    private GameObject Box(Transform parent,string name,float x,float y,float w,float h,Color color)
    {
        var go = Rect(parent,name,x,y,w,h).gameObject;
        go.AddComponent<Image>().color = color;
        return go;
    }
    private Text Label(Transform parent,string value,float x,float y,float w,float h,int size,bool bold=false)
    {
        var text = Rect(parent,"Text",x,y,w,h).gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = Color.black;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }
    private void Border(GameObject go)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(.4f,.4f,.4f);
        outline.effectDistance = new Vector2(1,-1);
    }
    private Button Button(Transform parent,string text,float x,float y,float w,float h,Action action)
    {
        var go = Box(parent,text,x,y,w,h,new Color(.9f,.9f,.9f));
        Border(go);
        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        var label = Label(go.transform,text,4,0,w-8,h,16);
        label.alignment = TextAnchor.MiddleCenter;
        button.onClick.AddListener(()=>action());
        return button;
    }
    private InputField Input(Transform parent,string value,float x,float y,float w,float h)
    {
        var go = Box(parent,"Input",x,y,w,h,Color.white);
        Border(go);
        var input = go.AddComponent<InputField>();
        var text = Label(go.transform,value,7,0,w-14,h,16);
        text.alignment = TextAnchor.MiddleLeft;
        input.textComponent = text;
        input.text = value;
        input.characterLimit = 64;
        input.targetGraphic = go.GetComponent<Image>();
        input.transition = Selectable.Transition.None;
        return input;
    }
    private void OnDestroy() { if (model != null) model.Changed -= Refresh; }
}

// Legacy Text builds its glyphs for the canvas scale that exists when it is first drawn.
// The CanvasScaler applies the real scale later (and again whenever the window is resized),
// so static labels would stay as a blurry, faded down-scaled copy. Just before the UI is drawn,
// re-generate every label if the scale changed (one float comparison; no Update loop).
public sealed class CanvasTextRefresher : MonoBehaviour
{
    private Canvas canvas;
    private float lastScale = -1;
    private void Awake() => canvas = GetComponent<Canvas>();
    private void OnEnable() => Canvas.preWillRenderCanvases += CheckScale;
    private void OnDisable() => Canvas.preWillRenderCanvases -= CheckScale;
    private void CheckScale()
    {
        if (canvas == null || Mathf.Approximately(canvas.scaleFactor, lastScale)) return;
        lastScale = canvas.scaleFactor;
        foreach (var text in GetComponentsInChildren<Text>(true)) text.SetAllDirty();
    }
}
