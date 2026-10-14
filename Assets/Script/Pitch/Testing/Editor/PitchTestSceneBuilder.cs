#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Homerunderby;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PitchTestSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/PitchTest.unity";
    private const string DataFolder = "Assets/Script/Pitch/Testing/SceneAssets";
    private static TMP_FontAsset _font;

    [MenuItem("Tools/Homerun Derby/投球テストシーンを作成")]
    public static void CreateFromMenu()
    {
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create();
    }

    public static void Create()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("既存の投球テストシーンは上書きしません。");
        Directory.CreateDirectory(DataFolder);
        AssetDatabase.Refresh();
        var sourceScene = EditorSceneManager.OpenScene("Assets/Scenes/HomeRun.unity", OpenSceneMode.Single);
        var original = Object.FindObjectsByType<PitcherController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        var originalData = new SerializedObject(original);
        var release = (Transform)originalData.FindProperty("_releasePoint").objectReferenceValue;
        var oldZone = (BoxCollider)originalData.FindProperty("_collider").objectReferenceValue;
        Bounds bounds = oldZone.bounds;
        var animation = (PitcherAnimationController)originalData.FindProperty("_animationController").objectReferenceValue;
        var animator = animation.GetComponent<Animator>();
        Transform pitcherRoot = original.transform.root;
        var oldBall = (PitchBallMove)originalData.FindProperty("_ballMove").objectReferenceValue;
        var ballRenderer = oldBall.GetComponentInChildren<MeshRenderer>(true);
        var ballMaterial = ballRenderer.sharedMaterial;
        var ballMesh = ballRenderer.GetComponent<MeshFilter>().sharedMesh;
        var ballScale = oldBall.transform.lossyScale;
        var oldBallRoot = oldBall.transform.root.gameObject;
        var oldZoneRoot = oldZone.transform.root.gameObject;
        float stopZ = originalData.FindProperty("_stopZ").floatValue;
        float deltaTime = originalData.FindProperty("_trajectorySettings.DeltaTime").floatValue;
        float maxSimulationTime = originalData.FindProperty("_trajectorySettings.MaxSimulationTime").floatValue;
        var list = originalData.FindProperty("_ballList");
        var pitches = new PitchTypeData[list.arraySize];
        for (int i = 0; i < pitches.Length; i++) pitches[i] = (PitchTypeData)list.GetArrayElementAtIndex(i).objectReferenceValue;
        if (pitches.Length == 0 || pitches.Any(p => p == null)) throw new InvalidOperationException("球種リストを確認してください。");

        // 手に付いていたボールも、テスト用の再生処理で持ち替える。
        Object.DestroyImmediate(oldBall.gameObject);

        // 球場と投手の見た目・配置を引き継ぎ、試合の進行を持つオブジェクトは外す。
        foreach (GameObject root in sourceScene.GetRootGameObjects())
        {
            if (root == pitcherRoot.gameObject) continue;
            bool environment = root.GetComponentInChildren<Light>(true) != null ||
                (root.GetComponentInChildren<Renderer>(true) != null &&
                 root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null &&
                 root.GetComponentInChildren<Canvas>(true) == null &&
                 root.GetComponentInChildren<Camera>(true) == null &&
                 root != oldBallRoot && root != oldZoneRoot);
            if (!environment) Object.DestroyImmediate(root);
        }
        foreach (GameObject root in sourceScene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Namespace == "UnityEngine.Rendering.Universal") continue;
                Object.DestroyImmediate(behaviour);
            }
            foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }
        pitcherRoot.name = "Test Pitcher";
        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.fireEvents = true;

        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ignore/HomeRunDerby/AA_Baseball_Pitcher_Idle_Loop_Type01.anim");
        var pitchClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ignore/HomeRunDerby/Pitcher_Throwing.anim");
        if (idle == null || pitchClip == null) throw new InvalidOperationException("投球・待機クリップがありません。");
        var releaseEvent = AnimationUtility.GetAnimationEvents(pitchClip).FirstOrDefault(e => e.functionName == "ReleasedBall");
        if (releaseEvent == null) throw new InvalidOperationException("ReleasedBallイベントがありません。");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(DataFolder + "/PitchTestPitcher.controller");
        var machine = controller.layers[0].stateMachine;
        var idleState = machine.AddState("Idle"); idleState.motion = idle;
        var pitchState = machine.AddState("Pitch"); pitchState.motion = pitchClip;
        machine.defaultState = idleState;
        animator.runtimeAnimatorController = controller;

        var test = new GameObject("Pitch Test").AddComponent<PitchTestSceneController>();
        animator.gameObject.AddComponent<PitchTestReleaseReceiver>().Configure(test);
        var testData = new SerializedObject(test);
        Set(testData, "_pitcherRoot", pitcherRoot);
        Set(testData, "_pitcherAnimator", animator);
        Set(testData, "_releasePoint", release);
        Set(testData, "_pitchAnimationSeconds", pitchClip.length);
        Set(testData, "_releaseEventSeconds", releaseEvent.time);
        Set(testData, "_stopZ", stopZ);
        var pitchProperty = testData.FindProperty("_pitchTypes"); pitchProperty.arraySize = pitches.Length;
        for (int i = 0; i < pitches.Length; i++) pitchProperty.GetArrayElementAtIndex(i).objectReferenceValue = pitches[i];
        testData.FindProperty("_settings.DeltaTime").floatValue = deltaTime;
        testData.FindProperty("_settings.MaxSimulationTime").floatValue = maxSimulationTime;

        var zone = new GameObject("Target Z Plane").AddComponent<BoxCollider>();
        zone.transform.position = bounds.center;
        zone.size = new Vector3(bounds.size.x, bounds.size.y, .01f);
        zone.isTrigger = true;
        Set(testData, "_strikeZone", zone);
        var markerMaterial = CreateMaterial("Markers", "Universal Render Pipeline/Particles/Unlit", Color.white);
        var line = AddLine("Strike Zone Outline", markerMaterial, new Color(.3f, 1, .85f), .012f);
        line.loop = true;
        line.positionCount = 4;
        line.SetPositions(new[] { new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.center.z), new Vector3(bounds.max.x, bounds.max.y, bounds.center.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.center.z) });
        Set(testData, "_hoverMarker", CreateMarker("Cursor Preview", markerMaterial, Color.cyan, .07f));
        Set(testData, "_targetMarker", CreateMarker("Confirmed Target", markerMaterial, new Color(1, .8f, .1f), .09f));
        Set(testData, "_crossMarker", CreateMarker("Actual Cross Point", markerMaterial, new Color(1, .25f, .7f), .045f));
        var trajectoryLine = AddLine("Pitch Trajectory", markerMaterial, new Color(1, .85f, .3f, .7f), .01f);
        Set(testData, "_trajectoryLine", trajectoryLine);
        var ballObject = new GameObject("Test Ball");
        var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(ballObject.transform, false);
        visual.GetComponent<MeshFilter>().sharedMesh = ballMesh;
        ballObject.transform.localScale = ballScale;
        visual.GetComponent<MeshRenderer>().sharedMaterial = ballMaterial;
        var scalerData = new SerializedObject(ballObject.AddComponent<BallDistanceVisualScaler>());
        scalerData.FindProperty("_visualTransform").objectReferenceValue = visual.transform;
        scalerData.ApplyModifiedPropertiesWithoutUndo();
        var ball = ballObject.AddComponent<PitchTestBallPlayer>();
        Set(testData, "_ball", ball);
        ball.ResetAt(release);

        CreateViews(testData, bounds, pitcherRoot.position);
        CreateUI(testData, bounds);
        AddComparisonTools(testData, bounds);
        ApplyReadableLayout(testData);
        testData.ApplyModifiedPropertiesWithoutUndo();
        test.RefreshPitchOptions();
        Directory.CreateDirectory("Artifacts/PitchTestScene");
        File.WriteAllText("Artifacts/PitchTestScene/scene-config.json", JsonUtility.ToJson(new SceneConfig {
            zoneCenter = bounds.center, zoneSize = bounds.size, releaseAtRest = release.position,
            rootPosition = pitcherRoot.position, releaseSeconds = releaseEvent.time, clipSeconds = pitchClip.length,
            pitchCount = pitches.Length }, true));
        EditorSceneManager.SaveScene(sourceScene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[投球テスト] {ScenePath}を作成。{pitches.Length}球種、リリースイベント={releaseEvent.time:F3}秒、ゾーン={bounds}。");
    }

    [Serializable] private class SceneConfig
    {
        public Vector3 zoneCenter, zoneSize, releaseAtRest, rootPosition;
        public float releaseSeconds, clipSeconds;
        public int pitchCount;
    }

    [MenuItem("Tools/Homerun Derby/投球テストに比較軌道と接近視点を追加")]
    private static void UpgradeComparisonFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        UpgradeComparison();
    }

    public static void UpgradeComparison()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var test = Object.FindFirstObjectByType<PitchTestSceneController>();
        if (test == null) throw new InvalidOperationException("投球テストの管理スクリプトがありません。");
        var data = new SerializedObject(test);
        var zone = (BoxCollider)data.FindProperty("_strikeZone").objectReferenceValue;
        AddComparisonTools(data, zone.bounds);
        ApplyReadableLayout(data);
        data.ApplyModifiedPropertiesWithoutUndo();
        test.RefreshPitchOptions();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[投球テスト] 無回転球との比較軌道、接近・横、接近・真上を設定しました。");
    }

    private static void AddComparisonTools(SerializedObject data, Bounds zone)
    {
        if (data.FindProperty("_referenceTrajectoryLine").objectReferenceValue != null) return;
        var actualLine = (LineRenderer)data.FindProperty("_trajectoryLine").objectReferenceValue;
        Set(data, "_referenceTrajectoryLine", AddLine("No Spin Reference Trajectory", actualLine.sharedMaterial,
            new Color(.25f, .65f, 1), .012f));

        var status = (TMP_Text)data.FindProperty("_statusText").objectReferenceValue;
        _font = status.font;
        var resultPanel = (RectTransform)status.transform.parent;
        resultPanel.sizeDelta = new Vector2(resultPanel.sizeDelta.x, 195);
        status.rectTransform.sizeDelta = new Vector2(618, 108);
        Set(data, "_comparisonText", Label(resultPanel,
            "黄線: 選択した球 / 青線: 同じ初速の無回転球\n接近・横 [F] で終盤の落下を確認できます。",
            new Vector2(16, -125), new Vector2(618, 58), 16));

        var dropdown = (TMP_Dropdown)data.FindProperty("_pitchDropdown").objectReferenceValue;
        var left = (RectTransform)dropdown.transform.parent;
        left.sizeDelta = new Vector2(330, 668);
        Place(((TMP_Text)data.FindProperty("_cameraText").objectReferenceValue).rectTransform,
            new Vector2(18, -210), new Vector2(294, 30));
        Set(data, "_closeViewButton", Button(left, "接近・横 [F]", new Vector2(18, -244), new Vector2(294, 30)));
        Place(((Button)data.FindProperty("_repeatButton").objectReferenceValue).GetComponent<RectTransform>(),
            new Vector2(18, -288), new Vector2(294, 42));
        Place(((Button)data.FindProperty("_resetButton").objectReferenceValue).GetComponent<RectTransform>(),
            new Vector2(18, -340), new Vector2(294, 38));
        var speedLabel = left.GetComponentsInChildren<TMP_Text>().First(t => t.text.StartsWith("再生速度"));
        Place(speedLabel.rectTransform, new Vector2(18, -390), new Vector2(294, 28));
        Place(((Slider)data.FindProperty("_playbackSlider").objectReferenceValue).GetComponent<RectTransform>(),
            new Vector2(18, -426), new Vector2(294, 24));
        Place(((Toggle)data.FindProperty("_trajectoryToggle").objectReferenceValue).GetComponent<RectTransform>(),
            new Vector2(18, -472), new Vector2(294, 32));
        Set(data, "_comparisonToggle", ComparisonToggle(left));
        var instructions = left.GetComponentsInChildren<TMP_Text>().First(t => t.text.StartsWith("マウス移動"));
        instructions.text = "マウス移動: 通過点を指定\n左クリック: 投球開始\n矢印キー: 視点切替 / F: 接近・横";
        instructions.fontSize = 17;
        Place(instructions.rectTransform, new Vector2(18, -552), new Vector2(294, 95));

        var views = data.FindProperty("_views");
        int firstCloseView = views.arraySize;
        views.arraySize += 2;
        data.FindProperty("_closeSideViewIndex").intValue = firstCloseView;
        Vector3 center = zone.center + new Vector3(0, .8f, -1.5f);
        for (int i = 0; i < 2; i++)
        {
            string name = i == 0 ? "接近・横" : "接近・真上";
            var camera = new GameObject($"Test Camera {firstCloseView + i + 1} - {name}").AddComponent<Camera>();
            camera.transform.position = center + (i == 0 ? new Vector3(4, 0, 0) : new Vector3(0, 10, 0));
            camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, i == 0 ? Vector3.up : Vector3.forward);
            camera.orthographic = true;
            camera.orthographicSize = 1.9f;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 500;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.gameObject.SetActive(false);
            var view = views.GetArrayElementAtIndex(firstCloseView + i);
            view.FindPropertyRelative("Name").stringValue = name;
            view.FindPropertyRelative("Camera").objectReferenceValue = camera;
        }
    }

    private static void ApplyReadableLayout(SerializedObject data)
    {
        var dropdown = (TMP_Dropdown)data.FindProperty("_pitchDropdown").objectReferenceValue;
        var left = (RectTransform)dropdown.transform.parent;
        var canvas = left.GetComponentInParent<Canvas>();
        // 高さを基準に縮小し、横長のGameビューでも上下のパネルの間隔を保つ。
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
        left.sizeDelta = new Vector2(380, 644);
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Max(14, text.fontSize - 2);
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        void Move(string field, float y, float height) => Place(
            ((Component)data.FindProperty(field).objectReferenceValue).GetComponent<RectTransform>(),
            new Vector2(18, -y), new Vector2(344, height));
        Move("_pitchDropdown", 92, 42);
        Place(((Button)data.FindProperty("_previousCameraButton").objectReferenceValue).GetComponent<RectTransform>(),
            new Vector2(18, -148), new Vector2(166, 36));
        Place(((Button)data.FindProperty("_nextCameraButton").objectReferenceValue).GetComponent<RectTransform>(),
            new Vector2(196, -148), new Vector2(166, 36));
        Move("_cameraText", 192, 30);
        Move("_closeViewButton", 230, 32);
        Move("_repeatButton", 274, 40);
        Move("_resetButton", 324, 36);
        Move("_playbackSlider", 410, 24);
        Move("_trajectoryToggle", 448, 30);
        Move("_comparisonToggle", 480, 30);
        var speed = left.GetComponentsInChildren<TMP_Text>().First(t => t.text.StartsWith("再生速度"));
        Place(speed.rectTransform, new Vector2(18, -374), new Vector2(344, 26));
        var pitchLabel = left.GetComponentsInChildren<TMP_Text>().First(t => t.text == "球種");
        Place(pitchLabel.rectTransform, new Vector2(18, -62), new Vector2(344, 26));
        var instructions = left.GetComponentsInChildren<TMP_Text>().First(t => t.text.StartsWith("マウス移動"));
        instructions.text = "マウス移動: 通過点を指定\n左クリック: 投球開始\n矢印キー: 視点切替\nF: 接近・横";
        Place(instructions.rectTransform, new Vector2(18, -516), new Vector2(344, 112));
        instructions.fontSizeMin = instructions.fontSizeMax = 16;
        foreach (var button in left.GetComponentsInChildren<Button>())
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            Place(label.rectTransform, new Vector2(4, -4), button.GetComponent<RectTransform>().sizeDelta - new Vector2(8, 8));
        }
        foreach (var toggle in left.GetComponentsInChildren<Toggle>())
        {
            var label = toggle.GetComponentInChildren<TMP_Text>();
            Place(label.rectTransform, new Vector2(40, 0), new Vector2(304, 30));
        }

        // 長い球種名を折り返すと一覧の次の行に重なるので、1行で収める。
        foreach (var label in new[] { dropdown.captionText, dropdown.itemText })
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSizeMin = 14;
            label.fontSizeMax = 18;
        }
        dropdown.template.sizeDelta = new Vector2(0, 252);
        dropdown.itemText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
        var scroll = dropdown.template.GetComponent<ScrollRect>();
        scroll.content.sizeDelta = new Vector2(0, 44);
        scroll.scrollSensitivity = 36;
        var arrow = dropdown.GetComponentsInChildren<TMP_Text>().First(t => t.text == "v");
        Place(arrow.rectTransform, new Vector2(310, -8), new Vector2(24, 26));

        var status = (TMP_Text)data.FindProperty("_statusText").objectReferenceValue;
        ((RectTransform)status.transform.parent).sizeDelta = new Vector2(740, 214);
        Place(status.rectTransform, new Vector2(16, -12), new Vector2(708, 128));
        status.fontSizeMin = 16; status.fontSizeMax = 18;
        var comparison = (TMP_Text)data.FindProperty("_comparisonText").objectReferenceValue;
        Place(comparison.rectTransform, new Vector2(16, -150), new Vector2(708, 52));
        var legend = canvas.GetComponentsInChildren<TMP_Text>().First(t => t.text.StartsWith("水色:"));
        legend.text = "水色: カーソル\n黄色: 指定点\n桃色: 通過点";
        Place(legend.rectTransform, new Vector2(16, -410), new Vector2(220, 66));
        ((RectTransform)legend.transform.parent).sizeDelta = new Vector2(250, 490);
    }

    private static Toggle ComparisonToggle(Transform parent)
    {
        var obj = new GameObject("Compare Fastball", typeof(RectTransform), typeof(Toggle));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        Place(rect, new Vector2(18, -512), new Vector2(294, 32));
        var background = Panel("Check Background", rect, Vector2.zero, new Vector2(26, 26), Color.gray);
        var check = Panel("Check", background, new Vector2(5, -5), new Vector2(16, 16), new Color(.25f, .65f, 1));
        var toggle = obj.GetComponent<Toggle>();
        toggle.targetGraphic = background.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();
        toggle.isOn = true;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        Label(rect, "無回転との比較を表示", new Vector2(40, 0), new Vector2(250, 32), 19);
        return toggle;
    }

    private static void Set(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
    private static void Set(SerializedObject data, string name, float value) => data.FindProperty(name).floatValue = value;

    private static Material CreateMaterial(string name, string shader, Color color)
    {
        var material = new Material(Shader.Find(shader));
        material.color = color;
        AssetDatabase.CreateAsset(material, $"{DataFolder}/{name}.mat");
        return material;
    }

    private static LineRenderer AddLine(string name, Material material, Color color, float width)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.positionCount = 0;
        line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private static Transform CreateMarker(string name, Material material, Color color, float radius)
    {
        var line = AddLine(name, material, color, .009f);
        const int count = 32;
        line.positionCount = count;
        line.useWorldSpace = false;
        line.loop = true;
        for (int i = 0; i < count; i++)
        {
            float angle = 2 * Mathf.PI * i / count;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -.005f));
        }
        return line.transform;
    }

    private static void CreateViews(SerializedObject data, Bounds zone, Vector3 pitcher)
    {
        Vector3 center = zone.center;
        float middleZ = (pitcher.z + center.z) * .5f;
        string[] names = { "捕手側", "投手側", "一塁側・真横", "三塁側・真横", "真上", "斜め前" };
        Vector3[] positions = { center + new Vector3(0, .7f, 5),
            new Vector3(pitcher.x - 1.6f, center.y + 1.1f, pitcher.z - 3.5f),
            new Vector3(16, 4, middleZ), new Vector3(-16, 4, middleZ),
            new Vector3(center.x, 23, middleZ), center + new Vector3(7, 5, 8) };
        var views = data.FindProperty("_views"); views.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var camera = new GameObject($"Test Camera {i + 1} - {names[i]}").AddComponent<Camera>();
            camera.transform.position = positions[i];
            Vector3 lookAt = i >= 2 ? new Vector3(center.x, center.y, middleZ) : center;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - positions[i], i == 4 ? Vector3.forward : Vector3.up);
            camera.fieldOfView = 55;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 500;
            camera.orthographic = i >= 2 && i <= 4;
            camera.orthographicSize = i == 4 ? 15 : 12.5f;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.tag = i == 0 ? "MainCamera" : "Untagged";
            camera.gameObject.SetActive(i == 0);
            var view = views.GetArrayElementAtIndex(i);
            view.FindPropertyRelative("Name").stringValue = names[i];
            view.FindPropertyRelative("Camera").objectReferenceValue = camera;
        }
    }

    private static void CreateUI(SerializedObject data, Bounds zone)
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ignore/TextMesh Pro/Fonts/NotoSansJP-VariableFont_wght.ttf");
        if (sourceFont == null) throw new InvalidOperationException("日本語UI用フォントがありません。");
        // 球種名を追加したときも文字を補充できる、テストUI専用のアトラスを用意する。
        _font = TMP_FontAsset.CreateFontAsset(sourceFont, 64, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        _font.name = "PitchTestJapanese";
        _font.material.SetFloat("_FaceDilate", .08f);
        AssetDatabase.CreateAsset(_font, DataFolder + "/PitchTestJapanese.asset");
        AssetDatabase.AddObjectToAsset(_font.material, _font);
        foreach (var atlas in _font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, _font);
        var canvasObject = new GameObject("Pitch Test UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        new GameObject("Test EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var left = Panel("Controls", canvas.transform, new Vector2(16, -16), new Vector2(330, 620), new Color(.04f, .08f, .12f, .93f));
        Label(left, "投球テスト", new Vector2(18, -16), new Vector2(294, 42), 28);
        Label(left, "球種", new Vector2(18, -70), new Vector2(294, 30), 18);
        var dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        dropdownObject.name = "Pitch Type";
        dropdownObject.transform.SetParent(left, false);
        Place(dropdownObject.GetComponent<RectTransform>(), new Vector2(18, -110), new Vector2(294, 42));
        foreach (TMP_Text text in dropdownObject.GetComponentsInChildren<TMP_Text>(true)) { text.font = _font; text.fontSize = 19; }
        var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdownObject.transform.Find("Arrow").GetComponent<Image>().enabled = false;
        var arrow = Label(dropdownObject.transform, "v", new Vector2(260, -8), new Vector2(24, 26), 18);
        arrow.alignment = TextAlignmentOptions.Center;
        arrow.color = new Color(.2f, .2f, .2f);
        Set(data, "_pitchDropdown", dropdown);
        // 矢印キーは視点切替に使い、UIの選択移動と競合させない。
        dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
        var next = Button(left, "次の視点 >", new Vector2(170, -168), new Vector2(142, 38));
        var previous = Button(left, "< 前の視点", new Vector2(18, -168), new Vector2(142, 38));
        Set(data, "_nextCameraButton", next); Set(data, "_previousCameraButton", previous);
        Set(data, "_cameraText", Label(left, "捕手側", new Vector2(18, -216), new Vector2(294, 34), 19));
        Set(data, "_repeatButton", Button(left, "同じ点へ再投球 [Space]", new Vector2(18, -268), new Vector2(294, 42)));
        Set(data, "_resetButton", Button(left, "リセット [R]", new Vector2(18, -320), new Vector2(294, 38)));
        Label(left, "再生速度  0.1 - 1.0倍", new Vector2(18, -370), new Vector2(294, 28), 18);
        var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderObject.transform.SetParent(left, false);
        Place(sliderObject.GetComponent<RectTransform>(), new Vector2(18, -406), new Vector2(294, 24));
        var slider = sliderObject.GetComponent<Slider>(); slider.minValue = .1f; slider.maxValue = 1; slider.value = 1;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        Set(data, "_playbackSlider", slider);
        var toggleObject = new GameObject("Show Trajectory", typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(left, false);
        var toggleRect = toggleObject.GetComponent<RectTransform>();
        Place(toggleRect, new Vector2(18, -452), new Vector2(294, 32));
        var background = Panel("Check Background", toggleRect, Vector2.zero, new Vector2(26, 26), Color.gray);
        var check = Panel("Check", background, new Vector2(5, -5), new Vector2(16, 16), new Color(.2f, .9f, .8f));
        var toggle = toggleObject.GetComponent<Toggle>(); toggle.targetGraphic = background.GetComponent<Image>(); toggle.graphic = check.GetComponent<Image>(); toggle.isOn = true;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        Label(toggleRect, "軌道を表示", new Vector2(40, 0), new Vector2(250, 32), 19);
        Set(data, "_trajectoryToggle", toggle);
        Label(left, "マウス移動: 通過点を指定\n左クリック: 投球開始\n矢印キー: 視点切替", new Vector2(18, -510), new Vector2(294, 95), 18);

        var right = Panel("Target Map Panel", canvas.transform, new Vector2(-16, -16), new Vector2(250, 470), new Color(.04f, .08f, .12f, .93f), true);
        Label(right, "通過点を指定", new Vector2(16, -16), new Vector2(220, 32), 22);
        Label(right, "全視点でクリックできます", new Vector2(16, -58), new Vector2(220, 30), 16);
        float height = Mathf.Min(280, 210 * zone.size.y / zone.size.x);
        float width = height * zone.size.x / zone.size.y;
        var map = Panel("Target Map", right, new Vector2((250 - width) * .5f, -112), new Vector2(width, height), new Color(.09f, .22f, .26f));
        Set(data, "_targetMap", map);
        for (int i = 1; i < 3; i++)
        {
            var vertical = Panel("Vertical Guide", map, new Vector2(width * i / 3, 0), new Vector2(1, height), new Color(.35f, .6f, .65f));
            var horizontal = Panel("Horizontal Guide", map, new Vector2(0, -height * i / 3), new Vector2(width, 1), new Color(.35f, .6f, .65f));
            vertical.GetComponent<Image>().raycastTarget = false; horizontal.GetComponent<Image>().raycastTarget = false;
        }
        Set(data, "_mapHoverMarker", MapMarker("Preview", map, Color.cyan, 13));
        Set(data, "_mapTargetMarker", MapMarker("Target", map, new Color(1, .8f, .1f), 11));
        Set(data, "_mapCrossMarker", MapMarker("Actual", map, new Color(1, .25f, .7f), 6));
        Label(right, "水色: カーソル\n黄色: 指定点 / 桃色: 通過点", new Vector2(16, -410), new Vector2(220, 56), 15);
        var statusPanel = Panel("Result Panel", canvas.transform, new Vector2(16, 16), new Vector2(650, 145), new Color(.04f, .08f, .12f, .93f));
        statusPanel.anchorMin = statusPanel.anchorMax = Vector2.zero; statusPanel.pivot = Vector2.zero;
        Set(data, "_statusText", Label(statusPanel, "球種と通過点を選択してください", new Vector2(16, -12), new Vector2(618, 122), 19));
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool right = false)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); Place(rect, position, size);
        if (right) { rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one; }
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    private static TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var obj = new GameObject(text.Split('\n')[0], typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        Place(obj.GetComponent<RectTransform>(), position, size);
        var label = obj.GetComponent<TextMeshProUGUI>(); label.font = _font; label.text = text; label.fontSize = fontSize;
        label.color = Color.white; label.raycastTarget = false; label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    private static Button Button(Transform parent, string text, Vector2 position, Vector2 size)
    {
        var rect = Panel(text, parent, position, size, new Color(.16f, .36f, .42f));
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var label = Label(rect, text, new Vector2(4, -4), size - new Vector2(8, 8), 18);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static RectTransform MapMarker(string name, Transform parent, Color color, int size)
    {
        var rect = Panel(name, parent, Vector2.zero, Vector2.one * size, color);
        rect.pivot = new Vector2(.5f, .5f); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.GetComponent<Image>().raycastTarget = false;
        return rect;
    }
}
#endif
