// JoystickRigTool.cs — Assets/Editor/
// Menus : Tools > Sous-Marin > Manche · … / Main · …
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JoystickRigTool
{
    const string MountName = "Manche_Droit";
    static string DiagDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../ClaudeDiag"));

    // ================================================================== MANCHE

    [MenuItem("Tools/Sous-Marin/Manche · Placer à droite du siège", false, 14)]
    public static void PlaceStick()
    {
        Directory.CreateDirectory(DiagDir);
        var log = new StringBuilder();
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        if (!ctl || !ctl.seatAnchor) { Debug.LogError("[Manche] SubmarineController ou seatAnchor manquant."); return; }
        var sub = ctl.transform;
        var all = sub.GetComponentsInChildren<Transform>(true);
        var handle = all.FirstOrDefault(t => t.name == "CockpitEquipments_Joystick2-Handle");
        var stickBase = all.FirstOrDefault(t => t.name == "CockpitEquipments_Joystick2-Base");
        if (!handle || !stickBase) { Debug.LogError("[Manche] Modèle du manche (CockpitEquipments_Joystick2-*) introuvable."); return; }

        // réglages précédents conservés si on relance
        float right = 0.40f, forward = 0.50f, below = 0.35f, yaw = 0f;
        var old = all.FirstOrDefault(t => t.name == MountName);
        if (old)
        {
            var om = old.GetComponent<JoystickMount>();
            if (om) { right = om.right; forward = om.forward; below = om.below; yaw = om.yaw; }
            Undo.DestroyObjectImmediate(old.gameObject);
            all = sub.GetComponentsInChildren<Transform>(true);   // la liste contenait l'ancien manche détruit
        }

        // 1) nouveau manche : même modèle que le tutoriel (disposition du vrai joystick), à l'échelle réelle
        var mount = new GameObject(MountName);
        Undo.RegisterCreatedObjectUndo(mount, "Manche à droite");
        mount.transform.SetParent(stickBase.parent, false);
        var jm = mount.AddComponent<JoystickMount>();
        jm.seatAnchor = ctl.seatAnchor; jm.submarine = sub;
        jm.right = right; jm.forward = forward; jm.below = below; jm.yaw = yaw;
        jm.gripTopHeight = 0.28f;
        jm.Apply();

        var model = new GameObject("Modele");
        model.layer = mount.layer;
        model.transform.SetParent(mount.transform, false);
        float s = JoystickModelBuilder.RealScale;
        model.transform.localScale = Vector3.one * s / Mathf.Max(1e-4f, mount.transform.lossyScale.x);
        var parts = JoystickModelBuilder.Build(model.transform, mount.layer);

        // pivot NON mis à l'échelle (la main s'y accroche en mètres) ; la poignée du modèle passe dessous
        var pivot = new GameObject("Pivot_Manche");
        pivot.transform.SetParent(mount.transform, false);
        pivot.transform.SetPositionAndRotation(parts.stickPivot.position, mount.transform.rotation);
        parts.stickPivot.SetParent(pivot.transform, true);
        parts.stickPivot.name = "Poignee";
        var jv = pivot.AddComponent<JoystickVisual>();
        jv.controller = ctl;

        // hauteur du sommet de la poignée au-dessus du pied → le haut de la poignée reste à la position voulue
        float topY = 0f;
        foreach (var mf in parts.stickPivot.GetComponentsInChildren<MeshFilter>(true))
        {
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                topY = Mathf.Max(topY, Vector3.Dot(mf.transform.TransformPoint(c) - mount.transform.position, sub.up));
            }
        }
        float gripTop = topY;
        jm.gripTopHeight = gripTop;
        jm.Apply();
        log.AppendLine($"Modèle du joystick réel à l'échelle {s:F3} : haut de poignée à {gripTop * 100f:F1} cm au-dessus du pied, rotation {yaw:F0}°");

        // colonne sous le socle : la partie sous la console est cachée par la console elle-même
        var col0 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        col0.name = "Colonne";
        Object.DestroyImmediate(col0.GetComponent<Collider>());
        col0.transform.SetParent(mount.transform, false);
        float colH = 0.6f, colR = 0.07f;
        col0.transform.localPosition = new Vector3(0f, -colH * 0.5f + 0.005f, 0f);
        col0.transform.localScale = new Vector3(colR * 2f, colH * 0.5f, colR * 2f);
        var frameRend = model.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Pied");
        if (frameRend) col0.GetComponent<MeshRenderer>().sharedMaterial = frameRend.sharedMaterial;

        // 2) masquer le manche central et les leviers de gaz décoratifs (réversible : cocher pour réafficher)
        var hide = all.Where(t => t.name == "CockpitEquipments_Joystick2-Handle" || t.name == "CockpitEquipments_Joystick2-Base"
                               || t.name.StartsWith("CockpitEquipments_ThrottleContro")).Select(t => t.gameObject).ToList();
        foreach (var g in hide)
        {
            Undo.RecordObject(g, "Manche à droite");
            g.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(g);
        }
        log.AppendLine("Masqués : " + string.Join(", ", hide.Select(g => g.name)));
        // l'ancien JoystickVisual éventuel
        foreach (var v in sub.GetComponentsInChildren<JoystickVisual>(true))
            if (v != jv && !v.transform.IsChildOf(mount.transform)) { Undo.RecordObject(v, "Manche à droite"); v.enabled = false; log.AppendLine($"Ancien JoystickVisual désactivé sur {v.name}"); }

        Vector3 top = sub.InverseTransformPoint(mount.transform.position) + Vector3.up * gripTop;
        Vector3 eye = sub.InverseTransformPoint(ctl.seatAnchor.position);
        log.AppendLine($"Manche posé : pied {sub.InverseTransformPoint(mount.transform.position):F3}  haut de poignée {top:F3}  (yeux {eye:F3} → écart {(top - eye):F3})");

        EditorSceneManager.MarkSceneDirty(sub.gameObject.scene);
        Selection.activeGameObject = mount;
        Renders(ctl, mount.transform.position + mount.transform.up * gripTop * 0.6f, "manche");
        File.WriteAllText(Path.Combine(DiagDir, "manche.txt"), log.ToString());
        Debug.Log("[Manche] Placé à droite.\n" + log);
    }

    const float uprightLeanDeg = 8f;

    static List<Vector3> GripPoints(Transform pivot, Transform grip)
    {
        var pts = new List<Vector3>();
        foreach (var mf in grip.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh)
                foreach (var v in mf.sharedMesh.vertices) pts.Add(pivot.InverseTransformPoint(mf.transform.TransformPoint(v)));
        return pts;
    }

    static Vector3 SliceCentroid(List<Vector3> pts, Vector3 axis, float a, float b)
    {
        float tMin = pts.Min(p => Vector3.Dot(p, axis)), tMax = pts.Max(p => Vector3.Dot(p, axis)), h = tMax - tMin;
        var s = pts.Where(p => { float t = Vector3.Dot(p, axis); return t >= tMin + h * a && t <= tMin + h * b; }).ToList();
        if (s.Count == 0) return Vector3.zero;
        return new Vector3(s.Average(p => p.x), s.Average(p => p.y), s.Average(p => p.z));
    }

    static void Uprighten(Transform pivot, Transform grip, float leanDeg, StringBuilder log)
    {
        var gripBody = grip.Find("Poignee_Corps");          // modèle du joystick réel : on analyse le corps de la poignée
        var pts = GripPoints(pivot, gripBody ? gripBody : grip);
        if (pts.Count < 10) return;
        // axe de la poignée : du pied (bas) vers le sommet (le plus loin du pied)
        Vector3 foot = SliceCentroid(pts, Vector3.up, 0f, 0.08f);
        Vector3 far = pts.OrderByDescending(p => (p - foot).sqrMagnitude).Take(Mathf.Max(5, pts.Count / 50)).Aggregate(Vector3.zero, (acc, p) => acc + p) / Mathf.Max(5, pts.Count / 50);
        Vector3 axis = (far - foot).normalized;
        Vector3 target = Quaternion.Euler(leanDeg, 0f, 0f) * Vector3.up;   // un peu vers l'avant
        Quaternion q = Quaternion.FromToRotation(axis, target);
        // rotation autour du pied, puis pied recentré sur le pivot
        Vector3 lp = grip.localPosition - foot;
        grip.localPosition = q * lp + foot;
        grip.localRotation = q * grip.localRotation;
        var pts2 = GripPoints(pivot, grip);
        Vector3 foot2 = SliceCentroid(pts2, Vector3.up, 0f, 0.08f);
        float yMin2 = pts2.Min(p => p.y);
        grip.localPosition += new Vector3(-foot2.x, -yMin2 - 0.005f, -foot2.z);
        log.AppendLine($"Poignée redressée : axe d'origine {axis:F2} (inclinaison {Vector3.Angle(axis, Vector3.up):F0}°) → {leanDeg:F0}° vers l'avant");
    }

    static Vector3 DivideScale(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

    static Bounds LocalBounds(Transform sub, Transform t)
    {
        bool has = false; var b = new Bounds();
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = sub.InverseTransformPoint(mf.transform.TransformPoint(c));
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    public static void Renders(SubmarineController ctl, Vector3 target, string prefix)
    {
        var sub = ctl.transform;
        Vector3 eye = ctl.seatAnchor.position;
        Vector3 fwd = Vector3.ProjectOnPlane(sub.forward, sub.up).normalized;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, prefix + "_regard.png"), 1400, 1000, eye, Quaternion.LookRotation(target - eye, sub.up), false, 0f, 70f, false);
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, prefix + "_vue_pilote.png"), 1400, 1000, eye, Quaternion.LookRotation(fwd, sub.up) * Quaternion.Euler(25f, 0f, 0f), false, 0f, 100f, false);
        Vector3 side = target + sub.right * 0.55f + sub.up * 0.15f - fwd * 0.1f;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, prefix + "_cote.png"), 1000, 1000, side, Quaternion.LookRotation(target - side, sub.up), false, 0f, 55f, false);
    }

    // ================================================================== MAIN : modèle XR Hands

    static AddRequest addReq;

    [MenuItem("Tools/Sous-Marin/Main · 1) Ajouter le paquet XR Hands", false, 15)]
    public static void AddHandsPackage()
    {
        addReq = Client.Add("com.unity.xr.hands");
        EditorApplication.update += WaitAdd;
        Debug.Log("[Main] Ajout du paquet com.unity.xr.hands…");
    }

    static void WaitAdd()
    {
        if (addReq == null) { EditorApplication.update -= WaitAdd; return; }
        if (!addReq.IsCompleted) return;
        EditorApplication.update -= WaitAdd;
        Directory.CreateDirectory(DiagDir);
        string msg = addReq.Status == StatusCode.Success
            ? $"OK : {addReq.Result.name} {addReq.Result.version}"
            : $"ÉCHEC : {addReq.Error?.message}";
        File.WriteAllText(Path.Combine(DiagDir, "main_paquet.txt"), msg);
        Debug.Log("[Main] Paquet XR Hands : " + msg);
    }

    [MenuItem("Tools/Sous-Marin/Main · 2) Importer l'exemple de mains", false, 16)]
    public static void ImportHandSample()
    {
        Directory.CreateDirectory(DiagDir);
        var sb = new StringBuilder();
        var info = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().FirstOrDefault(p => p.name == "com.unity.xr.hands");
        if (info == null) { File.WriteAllText(Path.Combine(DiagDir, "main_exemple.txt"), "Paquet XR Hands absent."); Debug.LogError("[Main] Paquet XR Hands absent."); return; }
        var samples = UnityEditor.PackageManager.UI.Sample.FindByPackage(info.name, info.version).ToList();
        foreach (var s in samples) sb.AppendLine($"Exemple : '{s.displayName}' importé={s.isImported} → {s.importPath}");
        var hv = samples.FirstOrDefault(s => s.displayName.ToLowerInvariant().Contains("visualizer"));
        if (hv.displayName == null) { sb.AppendLine("Exemple 'Hand Visualizer' introuvable."); }
        else
        {
            bool ok = hv.isImported || hv.Import(UnityEditor.PackageManager.UI.Sample.ImportOptions.OverridePreviousImports);
            sb.AppendLine($"Import Hand Visualizer : {ok} → {hv.importPath}");
            AssetDatabase.Refresh();
        }
        foreach (var g in AssetDatabase.FindAssets("Hand t:Model"))
            sb.AppendLine("Modèle : " + AssetDatabase.GUIDToAssetPath(g));
        File.WriteAllText(Path.Combine(DiagDir, "main_exemple.txt"), sb.ToString());
        Debug.Log("[Main] " + sb);
    }

    [MenuItem("Tools/Sous-Marin/Main · 3) Inspecter le modèle de main droite", false, 17)]
    public static void InspectHand()
    {
        Directory.CreateDirectory(DiagDir);
        var sb = new StringBuilder();
        var path = AssetDatabase.FindAssets("Hand t:Model").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.ToLowerInvariant().Contains("right")).OrderBy(p => p.Length).FirstOrDefault();
        if (path == null) { File.WriteAllText(Path.Combine(DiagDir, "main_os.txt"), "Aucun modèle de main droite."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var inst = (GameObject)Object.Instantiate(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        sb.AppendLine("Modèle : " + path);
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            sb.AppendLine($"SkinnedMesh '{smr.name}' os {smr.bones.Length} racine '{(smr.rootBone ? smr.rootBone.name : "-")}' bounds {smr.bounds.size:F3} mat '{(smr.sharedMaterial ? smr.sharedMaterial.shader.name : "-")}'");
        void Walk(Transform t, int d)
        {
            sb.AppendLine($"{new string(' ', d * 2)}{t.name}  pos {t.position:F4}  localRot {t.localRotation.eulerAngles:F1}");
            foreach (Transform c in t) Walk(c, d + 1);
        }
        Walk(inst.transform, 0);
        Object.DestroyImmediate(inst);
        File.WriteAllText(Path.Combine(DiagDir, "main_os.txt"), sb.ToString());
        Debug.Log("[Main] Os de la main → main_os.txt");
    }
    // ================================================================== MAIN : pose sur le manche

    const string HandName = "Main_Droite";
    const float HandYawDeg = 12f;   // la main est un peu tournée vers l'extérieur
    const string GloveMat = "Assets/Materials/Gant_Neoprene.mat";
    const string SleeveMat = "Assets/Materials/Manche_Combinaison.mat";

    [MenuItem("Tools/Sous-Marin/Main · 4) Poser la main sur le manche", false, 18)]
    public static void PlaceHand()
    {
        Directory.CreateDirectory(DiagDir);
        var log = new StringBuilder();
        var ctl = Object.FindAnyObjectByType<SubmarineController>(FindObjectsInactive.Include);
        var mountT = ctl ? ctl.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == MountName) : null;
        var pivot = mountT ? mountT.Find("Pivot_Manche") : null;
        var grip = pivot ? pivot.Find("Poignee") : null;
        if (!grip) { Debug.LogError("[Main] Manche_Droit/Pivot_Manche/Poignee introuvable : lancer d'abord « Manche · Placer à droite du siège »."); return; }
        var path = AssetDatabase.FindAssets("RightHand t:Model").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith("/RightHand.fbx")).FirstOrDefault();
        if (path == null) { Debug.LogError("[Main] RightHand.fbx introuvable : lancer « Main · 2) Importer l'exemple de mains »."); return; }

        // la pose se calcule manche au neutre
        var jv = pivot.GetComponent<JoystickVisual>();
        Quaternion pivotRot = pivot.localRotation;
        pivot.localRotation = Quaternion.identity;

        var old = pivot.Find(HandName);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);

        // ---- 1) géométrie de la poignée (repère du pivot)
        var gripBody = grip.Find("Poignee_Corps");          // modèle du joystick réel : on analyse le corps de la poignée
        var pts = GripPoints(pivot, gripBody ? gripBody : grip);
        if (pts.Count < 10) { Debug.LogError("[Main] Maillage de la poignée illisible."); pivot.localRotation = pivotRot; return; }
        float yMin = pts.Min(p => p.y), yMax = pts.Max(p => p.y), H = yMax - yMin;
        Vector3 Slice(float a, float b)
        {
            var s = pts.Where(p => p.y >= yMin + H * a && p.y <= yMin + H * b).ToList();
            if (s.Count == 0) return new Vector3(0, yMin + H * (a + b) * 0.5f, 0);
            return new Vector3(s.Average(p => p.x), s.Average(p => p.y), s.Average(p => p.z));
        }
        Vector3 cLow = Slice(0.2f, 0.4f), cMid = Slice(0.4f, 0.7f), cHigh = Slice(0.6f, 0.85f);
        Vector3 up = (cHigh - cLow).normalized;
        if (Vector3.Dot(up, Vector3.up) < 0.5f) up = Vector3.up;
        var midPts = pts.Where(p => p.y >= yMin + H * 0.4f && p.y <= yMin + H * 0.7f).ToList();
        var dists = midPts.Select(p => Vector3.ProjectOnPlane(p - cMid, up).magnitude).OrderBy(d => d).ToList();
        float r = Mathf.Clamp(dists.Count > 0 ? dists[(int)(dists.Count * 0.6f)] : 0.03f, 0.015f, 0.045f);
        Vector3 rightV = Vector3.ProjectOnPlane(Vector3.right, up).normalized;
        // la main est un peu tournée vers l'extérieur : l'avant-bras revient en diagonale vers le corps du pilote
        rightV = Quaternion.AngleAxis(HandYawDeg, up) * rightV;
        Vector3 fwd = Vector3.Cross(rightV, up).normalized;              // avant (Unity : x × y = z)
        float tTop = pts.Max(p => Vector3.Dot(p - cMid, up));
        Vector3 topC = cMid + up * (tTop * 0.97f);
        log.AppendLine($"Poignée : hauteur {H:F3} m, rayon ≈ {r:F3} m, axe {up:F2}, centre {cMid:F3}, sommet {topC:F3}");
        Vector3 At(float hFromTop, float thetaDeg, float rad)
        {
            float t = thetaDeg * Mathf.Deg2Rad;
            Vector3 c = topC - up * hFromTop;
            return c + (Mathf.Cos(t) * rightV + Mathf.Sin(t) * fwd) * rad;
        }

        // ---- 2) main
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(hand, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Undo.RegisterCreatedObjectUndo(hand, "Main sur le manche");
        hand.name = HandName;
        hand.transform.SetParent(pivot, false);
        // repère du modèle : doigts +Z, paume -Y, pouce côté -X  →  doigts vers l'avant, dos de la main vers l'extérieur,
        // index en haut / auriculaire en bas (poing fermé autour d'une poignée verticale)
        hand.transform.localRotation = Quaternion.LookRotation(fwd, rightV);
        float scale = 1.04f;
        hand.transform.localScale = Vector3.one * scale;
        Transform B(string n) => hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        var palm = B("R_Palm"); var midProx = B("R_MiddleProximal");
        Vector3 anchorHand = hand.transform.InverseTransformPoint((palm.position + midProx.position) * 0.5f);
        Vector3 gripPoint = topC - up * 0.055f + rightV * (r + 0.019f) - fwd * 0.004f;
        hand.transform.localPosition = gripPoint - hand.transform.localRotation * (anchorHand * scale);

        // ---- 3) doigts enroulés (CCD, rotation autour de l'axe de la poignée)
        Vector3 axisW = pivot.TransformDirection(-up);   // sens positif = vers la paume (vers la poignée)
        Vector3 W(Vector3 p) => pivot.TransformPoint(p);
        float HeightOf(Transform t) => Vector3.Dot(pivot.InverseTransformPoint(t.position) - topC, up) * -1f;
        Transform[] Chain(string f) => new[] { B($"R_{f}Proximal"), B($"R_{f}Intermediate"), B($"R_{f}Distal") };
        foreach (var f in new[] { "Middle", "Ring", "Little" })
        {
            var ch = Chain(f);
            float hft = HeightOf(ch[0]);
            Curl(ch, B($"R_{f}Tip"), W(At(hft + 0.004f, 185f, r + 0.007f)), axisW, -12f, 100f);
        }
        // index sur la gâchette (avant de la poignée, en haut) : pose repos puis enfoncée
        var idx = Chain("Index");
        float hIdx = HeightOf(idx[0]);
        var idxRelax0 = idx.Select(t => t.localRotation).ToArray();
        Curl(idx, B("R_IndexTip"), W(At(hIdx + 0.006f, 112f - HandYawDeg, r + 0.010f)), axisW, -12f, 80f);
        var idxRelax = idx.Select(t => t.localRotation).ToArray();
        Curl(idx, B("R_IndexTip"), W(At(hIdx + 0.008f, 140f - HandYawDeg, r - 0.002f)), axisW, -12f, 95f);
        var idxPress = idx.Select(t => t.localRotation).ToArray();
        for (int i = 0; i < idx.Length; i++) idx[i].localRotation = idxRelax[i];

        // pouce sur le côté gauche du sommet (bouton du pouce)
        var th = new[] { B("R_ThumbMetacarpal"), B("R_ThumbProximal"), B("R_ThumbDistal") };
        var thTip = B("R_ThumbTip");
        FreeReach(th, thTip, W(At(0.012f, 178f, r * 0.9f) + up * 0.004f), 55f);   // bouton du pouce : flanc gauche
        var thRelax = th.Select(t => t.localRotation).ToArray();
        FreeReach(th, thTip, W(At(0.012f, 178f, r * 0.75f) - up * 0.004f), 60f);
        var thPress = th.Select(t => t.localRotation).ToArray();
        for (int i = 0; i < th.Length; i++) th[i].localRotation = thRelax[i];

        // ---- 4) gant + manchette de combinaison
        var glove = GetOrMakeMat(GloveMat, new Color(0.13f, 0.14f, 0.16f), 0.5f);
        foreach (var smr in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = glove;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        var wrist = B("R_Wrist");
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sleeve.name = "Manchette";
        Object.DestroyImmediate(sleeve.GetComponent<Collider>());
        sleeve.transform.SetParent(wrist, false);
        float sl = 0.32f;   // avant-bras jusqu'au coude : bras tendu vers le manche
        // L'avant-bras part TOUJOURS vers l'épaule du pilote (en arrière, un peu vers le siège), quelle que soit
        // la rotation du manche : direction fixée dans les axes du sous-marin, puis convertie dans le repère du poignet.
        var subT = ctl.transform;
        Vector3 armWorld = (-0.92f * subT.forward - 0.38f * subT.right - 0.06f * subT.up).normalized;
        Vector3 armDir = wrist.InverseTransformDirection(armWorld).normalized;
        sleeve.transform.localRotation = Quaternion.FromToRotation(Vector3.up, armDir);
        sleeve.transform.localPosition = new Vector3(0f, -0.004f, 0.012f) + armDir * (sl * 0.5f);
        sleeve.transform.localScale = new Vector3(0.072f, sl * 0.5f, 0.062f);
        sleeve.GetComponent<MeshRenderer>().sharedMaterial = GetOrMakeMat(SleeveMat, new Color(0.10f, 0.14f, 0.20f), 0.35f);

        // ---- 5) animation des boutons
        var jh = hand.AddComponent<JoystickHand>();
        jh.indexJoints = idx; jh.indexRelaxed = idxRelax; jh.indexPressed = idxPress;
        jh.thumbJoints = th; jh.thumbRelaxed = thRelax; jh.thumbPressed = thPress;
        var stop = Object.FindAnyObjectByType<ComfortStop>(FindObjectsInactive.Include);
        if (stop) jh.indexButton = stop.stopButtonName;           // gâchette (pause)
        jh.thumbButton = ctl.recenterButtonName;                  // bouton du pouce (recalibrage)
        if (jh.indexButton == jh.thumbButton) jh.thumbButton = "button2";

        pivot.localRotation = pivotRot;
        EditorSceneManager.MarkSceneDirty(ctl.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = hand;
        log.AppendLine($"Main posée : échelle {scale}, index sur '{jh.indexButton}', pouce sur '{jh.thumbButton}'");
        foreach (var f in new[] { "Index", "Middle", "Ring", "Little", "Thumb" })
        {
            var tip = B($"R_{f}Tip");
            Vector3 lp = pivot.InverseTransformPoint(tip.position);
            float d = Vector3.ProjectOnPlane(lp - topC, up).magnitude;
            log.AppendLine($"  bout {f,-7} à {d * 100f:F1} cm de l'axe (rayon {r * 100f:F1}), {HeightOf(tip) * 100f:F1} cm sous le sommet");
        }

        // ---- 6) rendus de contrôle
        Vector3 target = pivot.TransformPoint(topC - up * 0.06f);
        Renders(ctl, target, "main");
        var sub = ctl.transform;
        Vector3 cam1 = target + sub.forward * 0.28f - sub.right * 0.12f + sub.up * 0.10f;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "main_face.png"), 900, 900, cam1, Quaternion.LookRotation(target - cam1, sub.up), false, 0f, 45f, false);
        Vector3 cam2 = target - sub.right * 0.30f + sub.up * 0.05f;
        CockpitMapInstaller.RenderTo(Path.Combine(DiagDir, "main_gauche.png"), 900, 900, cam2, Quaternion.LookRotation(target - cam2, sub.up), false, 0f, 45f, false);
        File.WriteAllText(Path.Combine(DiagDir, "main.txt"), log.ToString());
        Debug.Log("[Main] Main posée sur le manche.\n" + log);
    }

    static Material GetOrMakeMat(string path, Color c, float smooth)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(path)));
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Enroule une chaîne (proximale → distale) autour d'un axe pour amener le bout sur la cible.
    static void Curl(Transform[] joints, Transform tip, Vector3 target, Vector3 axis, float minDeg, float maxDeg)
    {
        var acc = new float[joints.Length];
        for (int it = 0; it < 30; it++)
            for (int j = joints.Length - 1; j >= 0; j--)
            {
                Vector3 jp = joints[j].position;
                Vector3 a = Vector3.ProjectOnPlane(tip.position - jp, axis), b = Vector3.ProjectOnPlane(target - jp, axis);
                if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) continue;
                float ang = Vector3.SignedAngle(a, b, axis) * 0.6f;
                float next = Mathf.Clamp(acc[j] + ang, minDeg, maxDeg);
                ang = next - acc[j]; acc[j] = next;
                joints[j].rotation = Quaternion.AngleAxis(ang, axis) * joints[j].rotation;
            }
    }

    // Atteinte libre (pouce) : rotation sur n'importe quel axe, limitée par articulation.
    static void FreeReach(Transform[] joints, Transform tip, Vector3 target, float maxDeg)
    {
        var start = joints.Select(t => t.rotation).ToArray();
        for (int it = 0; it < 30; it++)
            for (int j = joints.Length - 1; j >= 0; j--)
            {
                Vector3 jp = joints[j].position;
                Vector3 a = tip.position - jp, b = target - jp;
                if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) continue;
                Quaternion q = Quaternion.FromToRotation(a, b);
                q.ToAngleAxis(out float ang, out Vector3 ax);
                if (ang > 180f) ang -= 360f;
                Quaternion cand = Quaternion.AngleAxis(ang * 0.5f, ax) * joints[j].rotation;
                if (Quaternion.Angle(start[j], cand) <= maxDeg) joints[j].rotation = cand;
            }
    }
}
#endif
