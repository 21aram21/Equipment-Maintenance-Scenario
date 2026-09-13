using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ВАЖНО: это Editor-скрипт. Файл должен лежать в папке с именем "Editor"
/// (например Assets/Project/Editor/ProcedureCanvasBuilder.cs), иначе он
/// попадёт в билд и не скомпилируется под Android/Pico.
///
/// Одной командой (Tools -> ProcTrain VR -> Create Procedure Canvas)
/// собирает World Space Canvas со всей иерархией (подсказка, прогресс
/// удержания на шаге 3, финальная панель) и вешает/связывает UIManager.
///
/// Компоненты, специфичные для XR Interaction Toolkit (Tracked Device
/// Graphic Raycaster на Canvas, UI Input Module на EventSystem),
/// добавляются через поиск типа по имени, а не через прямой using —
/// точное имя класса менялось между версиями XRIT, и жёсткая ссылка на
/// несуществующий тип уронила бы компиляцию всего проекта. Если тип не
/// найден, скрипт логирует предупреждение — тогда добавь компонент руками.
/// </summary>
public static class ProcedureCanvasBuilder
{
    private const string RootName = "ProcedureCanvas";

    [MenuItem("Tools/ProcTrain VR/Create Procedure Canvas")]
    public static void CreateProcedureCanvas()
    {
        if (GameObject.Find(RootName) != null)
        {
            Debug.LogWarning($"'{RootName}' уже есть в сцене — удали его или переименуй, если нужен ещё один.");
            return;
        }

        EnsureEventSystem();

        GameObject canvasGO = new GameObject(RootName, typeof(RectTransform));
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGO.AddComponent<GraphicRaycaster>();

        TryAddComponentByTypeName(canvasGO, new[]
        {
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster"
        }, "Tracked Device Graphic Raycaster (клик по Canvas лучом контроллера)");

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800, 400);
        canvasGO.transform.localScale = Vector3.one * 0.001f;
        canvasGO.transform.position = new Vector3(0f, 1.5f, 1f);

        // ----- Hint -----
        GameObject hintPanel = CreatePanel(canvasGO.transform, "HintPanel", new Vector2(700, 100), new Color(0, 0, 0, 0.6f));
        TMP_Text hintText = CreateText(hintPanel.transform, "HintText", "Подсказка", 36, Vector2.zero);

        // ----- Hold progress (шаг 3) -----
        GameObject holdPanel = CreatePanel(canvasGO.transform, "HoldProgressPanel", new Vector2(500, 40), new Color(0, 0, 0, 0.4f));
        Image holdFill = CreateFillBar(holdPanel.transform, "HoldProgressFill", new Vector2(480, 24));

        // ----- Success (шаг 4) -----
        GameObject successPanel = CreatePanel(canvasGO.transform, "SuccessPanel", new Vector2(700, 300), new Color(0, 0, 0, 0.85f));
        CreateText(successPanel.transform, "SuccessText", "Процедура выполнена успешно!", 42, new Vector2(0, 80));
        Button restartButton = CreateButton(successPanel.transform, "RestartButton", "Restart", new Vector2(-150, -60));
        Button menuButton = CreateButton(successPanel.transform, "MenuButton", "Menu", new Vector2(150, -60));

        // ----- UIManager -----
        UIManager uiManager = canvasGO.AddComponent<UIManager>();
        SerializedObject so = new SerializedObject(uiManager);
        so.FindProperty("hintPanel").objectReferenceValue = hintPanel;
        so.FindProperty("hintText").objectReferenceValue = hintText;
        so.FindProperty("holdProgressPanel").objectReferenceValue = holdPanel;
        so.FindProperty("holdProgressFill").objectReferenceValue = holdFill;
        so.FindProperty("successPanel").objectReferenceValue = successPanel;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(restartButton.onClick, uiManager.OnRestartButtonPressed);
        UnityEventTools.AddPersistentListener(menuButton.onClick, uiManager.OnMenuButtonPressed);

        hintPanel.SetActive(false);
        holdPanel.SetActive(false);
        successPanel.SetActive(false);

        Selection.activeGameObject = canvasGO;

        Debug.Log(
            "ProcedureCanvas создан. Осталось руками: " +
            "1) назначить Procedure Manager в UIManager (сам скрипт его не знает — он сценоспецифичен); " +
            "2) проверить в Console, что оба XR-компонента реально добавились (см. предупреждения ниже, если есть); " +
            "3) включить 'Enable UI Interaction' на Ray Interactor контроллера; " +
            "4) связать UnityEvent'ы PartStationController.hintRequested / " +
            "HoldToActivateController.wrongOrderHintRequested / holdProgressChanged " +
            "с методами UIManager.ShowHint / UpdateHoldProgress вручную в инспекторе.");
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();

        TryAddComponentByTypeName(esGO, new[]
        {
            "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule",
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
            "UnityEngine.EventSystems.StandaloneInputModule"
        }, "UI Input Module (обработка ввода для Canvas)");
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color background)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        go.GetComponent<Image>().color = background;

        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform parentRect = parent.GetComponent<RectTransform>();
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = parentRect.sizeDelta * 0.9f;
        rect.anchoredPosition = anchoredPos;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        return text;
    }

    private static Image CreateFillBar(Transform parent, string name, Vector2 size)
    {
        GameObject bgGO = new GameObject(name + "_Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(parent, false);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.sizeDelta = size;
        bgGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        GameObject fillGO = new GameObject(name, typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(bgGO.transform, false);
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillGO.GetComponent<Image>();
        fillImage.color = new Color(0.2f, 0.8f, 0.3f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;

        return fillImage;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220, 70);
        rect.anchoredPosition = anchoredPos;

        go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        CreateText(go.transform, name + "_Label", label, 28, Vector2.zero);

        return go.GetComponent<Button>();
    }

    private static void TryAddComponentByTypeName(GameObject go, string[] candidateTypeNames, string friendlyName)
    {
        foreach (string typeName in candidateTypeNames)
        {
            Type type = FindTypeByName(typeName);
            if (type == null) continue;

            if (go.GetComponent(type) == null)
                go.AddComponent(type);

            Debug.Log($"[ProcedureCanvasBuilder] Добавлен '{type.FullName}' на '{go.name}'.");
            return;
        }

        Debug.LogWarning(
            $"[ProcedureCanvasBuilder] Не найден ни один тип для '{friendlyName}' " +
            $"(искали: {string.Join(", ", candidateTypeNames)}). " +
            $"Добавь компонент вручную на '{go.name}' — имя класса могло отличаться " +
            $"в установленной версии XR Interaction Toolkit / Input System.");
    }

    private static Type FindTypeByName(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null) return type;
        }
        return null;
    }
}
