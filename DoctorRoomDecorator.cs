using System.Collections;
using UnityEngine;

/// <summary>
/// DoctorRoomDecorator v3
///
/// Room orientation (AIRoomBox, camera looks toward +Z):
///   Player stands near BACK wall (Z = -5), looking toward +Z
///   Doctor  sits near FRONT wall (Z = +5)
///   Desk    at Z ≈ 0.5  (between them)
///   Left  wall  X = -5  (window)
///   Right wall  X = +5  (6 art pictures)
///
/// Fixes in this version:
///   • Procedural Rothko-style paintings — no internet required, always load
///   • Doctor's chair is now on the +Z side of desk, facing the patient (-Z)
///   • Patient chairs on the -Z side, facing the desk (+Z)
///   • Bookshelf moved to the FRONT wall (doctor's side)
///   • Wall colour updated to warm cream in room_config.json
/// </summary>
public class DoctorRoomDecorator : MonoBehaviour
{
    const float H   =  5f;     // half of floorSize (10m room)
    const float FLR = -2.5f;   // floor Y
    const float CEIL = 2.5f;   // ceiling Y

    /// <summary>
    /// Populated after Start() — the 6 canvas MeshRenderers on the right wall,
    /// indexed 0-5 in row-major order: [0..2] top row (Z=-2,0,+2), [3..5] bottom row.
    /// WallImageQuiz reads this to apply downloaded textures.
    /// </summary>
    public static MeshRenderer[] PictureCanvases = new MeshRenderer[3];

    // ── Auto-spawn ────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        var go = new GameObject("DoctorRoomDecorator");
        DontDestroyOnLoad(go);
        go.AddComponent<DoctorRoomDecorator>();
    }

    void Start()
    {
        // Fix green camera background — set to dark neutral colour
        if (Camera.main != null)
        {
            Camera.main.clearFlags       = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor  = new Color(0.06f, 0.07f, 0.10f);
        }

        BuildRug();
        BuildDesk();
        BuildPatientChairs();
        BuildWindow();
        BuildBookshelf();          // on FRONT wall — doctor's side
        BuildFilingCabinet();
        BuildFloorLamp();
        BuildAnatomyPoster();
        BuildPictures();           // procedural paintings, no internet needed
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // RUG  — dark teal area rug centred under desk zone
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildRug()
    {
        var rug = GameObject.CreatePrimitive(PrimitiveType.Quad);
        rug.name = "AreaRug";
        rug.transform.position    = new Vector3(0, FLR + 0.005f, 0.5f);
        rug.transform.eulerAngles = new Vector3(90, 0, 0);
        rug.transform.localScale  = new Vector3(7.2f, 5.5f, 1f);
        Destroy(rug.GetComponent<Collider>());
        rug.GetComponent<MeshRenderer>().material = Mat(new Color(0.09f, 0.20f, 0.26f));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DESK  — large walnut desk, centred, doctor faces player (-Z side)
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildDesk()
    {
        var root = new GameObject("Desk");

        float dW = 5.0f, dD = 1.8f, dH = 0.76f;
        float topY = FLR + dH;
        float dZ   = 0.5f;   // desk centre Z
        float lW   = 0.10f;

        // Table top
        Box(root, "Top", new Vector3(0, topY + 0.03f, dZ), new Vector3(dW, 0.07f, dD), Wood());

        // 4 legs
        float lx = dW / 2f - lW * 0.7f, lz = dD / 2f - lW * 0.7f;
        foreach (var p in new[] {
            new Vector3(-lx, 0, -lz), new Vector3(lx, 0, -lz),
            new Vector3(-lx, 0,  lz), new Vector3(lx, 0,  lz) })
            Box(root, "Leg", new Vector3(p.x, FLR + dH / 2f, dZ + p.z), new Vector3(lW, dH, lW), DkWood());

        // Front modesty panel  (patient side = -Z side of desk)
        Box(root, "Panel",
            new Vector3(0, FLR + dH * 0.45f, dZ - dD / 2f + 0.015f),
            new Vector3(dW - lW * 3f, dH * 0.82f, 0.05f), DkWood());

        // ── Accessories on desk surface ───────────────────────────────────────
        Box(root, "Papers",  new Vector3(-1.5f, topY + 0.04f, dZ + 0.10f), new Vector3(0.55f, 0.04f, 0.42f), Mat(new Color(0.97f, 0.97f, 0.94f)));
        Box(root, "Papers2", new Vector3(-1.5f, topY + 0.07f, dZ + 0.07f), new Vector3(0.48f, 0.025f, 0.35f), Mat(new Color(0.88f, 0.91f, 0.95f)));

        Cyl(root, "PenHolder", new Vector3(1.8f, topY + 0.10f, dZ + 0.16f), new Vector3(0.09f, 0.10f, 0.09f), Mat(new Color(0.72f, 0.62f, 0.42f)));
        for (int i = 0; i < 3; i++)
            Cyl(root, $"Pen{i}", new Vector3(1.8f + (i - 1) * 0.025f, topY + 0.22f, dZ + 0.16f), new Vector3(0.012f, 0.10f, 0.012f), Mat(new Color(0.1f, 0.1f, 0.6f)));

        // Monitor (doctor-side, facing patient)
        Box(root, "MonBase",   new Vector3(0.2f, topY + 0.04f, dZ + 0.35f), new Vector3(0.50f, 0.07f, 0.35f), Mat(new Color(0.10f, 0.10f, 0.10f)));
        Box(root, "MonNeck",   new Vector3(0.2f, topY + 0.18f, dZ + 0.42f), new Vector3(0.04f, 0.24f, 0.04f), Mat(new Color(0.10f, 0.10f, 0.10f)));
        Box(root, "MonScreen", new Vector3(0.2f, topY + 0.34f, dZ + 0.48f), new Vector3(0.68f, 0.44f, 0.028f), Mat(new Color(0.04f, 0.08f, 0.15f)));

        // Stethoscope coil
        Sph(root, "Stetho", new Vector3(-0.6f, topY + 0.06f, dZ + 0.05f), new Vector3(0.18f, 0.04f, 0.18f), Mat(new Color(0.12f, 0.12f, 0.12f)));

        // Mug, succulent, tissue box
        Cyl(root, "Mug",     new Vector3(1.1f,  topY + 0.07f, dZ + 0.10f), new Vector3(0.09f, 0.08f, 0.09f), Mat(new Color(0.85f, 0.85f, 0.85f)));
        Cyl(root, "Pot",     new Vector3(-2.1f, topY + 0.08f, dZ + 0.10f), new Vector3(0.11f, 0.08f, 0.11f), Mat(new Color(0.58f, 0.40f, 0.24f)));
        Sph(root, "Plant",   new Vector3(-2.1f, topY + 0.20f, dZ + 0.10f), new Vector3(0.17f, 0.17f, 0.17f), Mat(new Color(0.18f, 0.52f, 0.14f)));
        Box(root, "Tissues", new Vector3(2.1f,  topY + 0.07f, dZ + 0.06f), new Vector3(0.18f, 0.11f, 0.12f), Mat(new Color(0.95f, 0.85f, 0.78f)));

        // ── Doctor's chair — on the +Z side of desk, facing patient (-Z) ──────
        // footPos Z = dZ + dD/2 + gap
        BuildDoctorChair(root, new Vector3(0, FLR, dZ + dD / 2f + 0.70f));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PATIENT CHAIRS  — on -Z side of desk, facing +Z (toward desk/doctor)
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildPatientChairs()
    {
        var root = new GameObject("PatientChairs");
        float dZ    = 0.5f;
        float dHalf = 0.9f;  // dD/2
        float chairZ = dZ - dHalf - 0.80f;   // -Z side of desk, with gap

        BuildSimpleChair(root, new Vector3(-1.1f, FLR, chairZ));  // faces +Z by default
        BuildSimpleChair(root, new Vector3( 1.1f, FLR, chairZ));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WINDOW  — left wall (X = -5), large 4-pane daylight window
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildWindow()
    {
        float wx = -(H - 0.06f);  // just inside left wall
        float cy = 0.50f;
        float wW = 3.6f, wH = 2.6f, ft = 0.07f;
        Color fc = new Color(0.18f, 0.18f, 0.18f);

        var root = new GameObject("Window_Left");

        // Sky pane
        var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sky.name = "Sky";
        sky.transform.SetParent(root.transform);
        sky.transform.position    = new Vector3(wx + 0.04f, cy, 0f);
        sky.transform.localScale  = new Vector3(wW - ft * 2f, wH - ft * 2f, 1f);
        sky.transform.eulerAngles = new Vector3(0, 90, 0);
        Destroy(sky.GetComponent<Collider>());
        sky.GetComponent<MeshRenderer>().material = Mat(new Color(0.78f, 0.92f, 1.00f));

        // Outer frame
        float hw = wW / 2f, hh = wH / 2f;
        Bar(root, new Vector3(wx + ft, cy + hh,  0), new Vector3(ft, ft, wW + ft), fc);
        Bar(root, new Vector3(wx + ft, cy - hh,  0), new Vector3(ft, ft, wW + ft), fc);
        Bar(root, new Vector3(wx + ft, cy, +hw),     new Vector3(ft, wH + ft, ft), fc);
        Bar(root, new Vector3(wx + ft, cy, -hw),     new Vector3(ft, wH + ft, ft), fc);

        // Cross dividers
        Bar(root, new Vector3(wx + ft, cy, 0), new Vector3(ft, wH, ft), fc);  // vertical mid
        Bar(root, new Vector3(wx + ft, cy, 0), new Vector3(ft, ft, wW), fc);  // horizontal mid

        // Sill + venetian blind bundle
        Box(root, "Sill",  new Vector3(wx + 0.17f, cy - hh - 0.04f, 0f), new Vector3(0.28f, 0.055f, wW + 0.22f), Mat(new Color(0.90f, 0.86f, 0.80f)));
        Box(root, "Blind", new Vector3(wx + 0.05f, cy + hh - ft - 0.30f, 0f), new Vector3(ft * 0.5f, 0.48f, wW - ft * 2.5f), Mat(new Color(0.93f, 0.91f, 0.84f)));

        // Sill plant
        float sy = cy - hh - 0.015f;
        Cyl(root, "SillPot",   new Vector3(wx + 0.22f, sy + 0.09f, 0.6f), new Vector3(0.14f, 0.10f, 0.14f), Mat(new Color(0.52f, 0.36f, 0.20f)));
        Sph(root, "SillPlant", new Vector3(wx + 0.22f, sy + 0.24f, 0.6f), new Vector3(0.20f, 0.20f, 0.20f), Mat(new Color(0.16f, 0.50f, 0.12f)));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BOOKSHELF  — FRONT wall (doctor's side, Z ≈ +5), visible to player
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildBookshelf()
    {
        var root = new GameObject("Bookshelf");

        // Against the front wall (Z = +H = +5)
        float bz = H - 0.20f;        // close to front wall
        float bx = H - 1.50f;        // right-ish side of front wall (as player sees it)
        float bW = 2.0f;             // width in X
        float bH = 3.4f;             // height
        float bD = 0.45f;            // depth (extends toward -Z)

        // Back panel (against front wall, thin slab in Z)
        Box(root, "BackPanel",
            new Vector3(bx, FLR + bH / 2f, bz - 0.015f),
            new Vector3(bW, bH, 0.04f), DkWood());

        // Side panels
        Box(root, "SideL", new Vector3(bx - bW / 2f + 0.02f, FLR + bH / 2f, bz - bD / 2f), new Vector3(0.04f, bH, bD), DkWood());
        Box(root, "SideR", new Vector3(bx + bW / 2f - 0.02f, FLR + bH / 2f, bz - bD / 2f), new Vector3(0.04f, bH, bD), DkWood());

        // Top & base
        Box(root, "Top",  new Vector3(bx, FLR + bH,        bz - bD / 2f), new Vector3(bW, 0.04f, bD), DkWood());
        Box(root, "Base", new Vector3(bx, FLR + 0.02f,     bz - bD / 2f), new Vector3(bW, 0.04f, bD), DkWood());

        // 4 shelves with books
        Color[] bookC = {
            new Color(0.65f, 0.15f, 0.15f), new Color(0.15f, 0.30f, 0.58f),
            new Color(0.20f, 0.50f, 0.20f), new Color(0.68f, 0.55f, 0.12f),
            new Color(0.45f, 0.20f, 0.50f), new Color(0.18f, 0.45f, 0.48f),
            new Color(0.58f, 0.35f, 0.15f), new Color(0.48f, 0.48f, 0.48f),
        };
        for (int s = 0; s < 4; s++)
        {
            float sy = FLR + 0.70f + s * 0.76f;
            Box(root, $"Shelf{s}", new Vector3(bx, sy, bz - bD / 2f), new Vector3(bW - 0.08f, 0.03f, bD - 0.04f), Wood());

            // Books (standing up, faces -Z toward player)
            float bookX = bx - bW / 2f + 0.12f;
            for (int b = 0; b < 9; b++)
            {
                float bkW  = Random.Range(0.09f, 0.16f);
                float bkH  = Random.Range(0.26f, 0.42f);
                float bkD  = bD - 0.10f;
                Box(root, $"Book{s}_{b}",
                    new Vector3(bookX + bkW / 2f, sy + bkH / 2f + 0.015f, bz - bD / 2f),
                    new Vector3(bkW, bkH, bkD),
                    Mat(bookC[(s * 9 + b) % bookC.Length]));
                bookX += bkW + 0.01f;
                if (bookX > bx + bW / 2f - 0.14f) break;
            }
        }

        // Small framed certificate above shelf (on front wall)
        Box(root, "CertFrame", new Vector3(bx - 1.20f, 0.80f, bz - 0.02f), new Vector3(0.70f, 0.52f, 0.02f), Mat(new Color(0.55f, 0.42f, 0.18f)));
        Box(root, "CertPaper", new Vector3(bx - 1.20f, 0.80f, bz - 0.03f), new Vector3(0.60f, 0.42f, 0.015f), Mat(new Color(0.97f, 0.95f, 0.88f)));
        Box(root, "CertTitle", new Vector3(bx - 1.20f, 1.00f, bz - 0.04f), new Vector3(0.48f, 0.045f, 0.01f), Mat(new Color(0.18f, 0.30f, 0.52f)));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FILING CABINET  — back-right corner (patient side)
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildFilingCabinet()
    {
        var root = new GameObject("FilingCabinet");
        float cx = H - 0.55f;         // right wall side
        float cz = -(H - 0.65f);      // near back wall
        float cW = 0.55f, cH = 1.30f, cD = 0.65f;

        Box(root, "Body", new Vector3(cx, FLR + cH / 2f, cz), new Vector3(cD, cH, cW), Mat(new Color(0.55f, 0.57f, 0.60f)));

        for (int i = 0; i < 3; i++)
        {
            float dy = FLR + 0.18f + i * 0.38f;
            Box(root, $"Drawer{i}", new Vector3(cx + cD / 2f - 0.01f, dy, cz), new Vector3(0.02f, 0.32f, cW - 0.06f), Mat(new Color(0.50f, 0.52f, 0.55f)));
            Cyl(root, $"Handle{i}", new Vector3(cx + cD / 2f + 0.015f, dy, cz), new Vector3(0.025f, 0.015f, 0.14f), Mat(new Color(0.75f, 0.72f, 0.65f)));
        }

        Cyl(root, "Pot",   new Vector3(cx, FLR + cH + 0.09f, cz), new Vector3(0.10f, 0.09f, 0.10f), Mat(new Color(0.55f, 0.38f, 0.22f)));
        Sph(root, "Plant", new Vector3(cx, FLR + cH + 0.22f, cz), new Vector3(0.15f, 0.15f, 0.15f), Mat(new Color(0.20f, 0.55f, 0.16f)));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FLOOR LAMP  — beside window (left wall)
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildFloorLamp()
    {
        var root = new GameObject("FloorLamp");
        float lx = -(H - 0.70f);
        float lz = -2.2f;

        Cyl(root, "Base",  new Vector3(lx, FLR + 0.04f, lz), new Vector3(0.30f, 0.04f, 0.30f), Mat(new Color(0.20f, 0.20f, 0.20f)));
        Cyl(root, "Pole",  new Vector3(lx, FLR + 1.10f, lz), new Vector3(0.04f, 1.10f, 0.04f), Mat(new Color(0.28f, 0.28f, 0.28f)));
        Box(root,  "Arm",  new Vector3(lx + 0.22f, FLR + 2.10f, lz), new Vector3(0.46f, 0.03f, 0.03f), Mat(new Color(0.28f, 0.28f, 0.28f)));
        Cyl(root,  "Shade", new Vector3(lx + 0.42f, FLR + 1.96f, lz), new Vector3(0.32f, 0.14f, 0.32f), Mat(new Color(0.92f, 0.88f, 0.72f)));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ANATOMY POSTER  — back wall (behind patient, Z = -5)
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildAnatomyPoster()
    {
        var root = new GameObject("AnatomyPoster");
        float pz = -(H - 0.06f);

        Box(root, "Board", new Vector3(-2.8f, 0.3f, pz + 0.02f), new Vector3(0.90f, 1.40f, 0.015f), Mat(new Color(0.94f, 0.92f, 0.86f)));

        Material bodyMat = Mat(new Color(0.75f, 0.40f, 0.35f));
        Sph(root, "Head",  new Vector3(-2.8f, 0.88f, pz + 0.04f), new Vector3(0.14f, 0.17f, 0.06f), bodyMat);
        Box(root, "Torso", new Vector3(-2.8f, 0.54f, pz + 0.04f), new Vector3(0.22f, 0.32f, 0.03f), bodyMat);
        Box(root, "ArmL",  new Vector3(-2.94f, 0.46f, pz + 0.04f), new Vector3(0.08f, 0.30f, 0.03f), bodyMat);
        Box(root, "ArmR",  new Vector3(-2.66f, 0.46f, pz + 0.04f), new Vector3(0.08f, 0.30f, 0.03f), bodyMat);
        Box(root, "LegL",  new Vector3(-2.87f, 0.16f, pz + 0.04f), new Vector3(0.09f, 0.26f, 0.03f), bodyMat);
        Box(root, "LegR",  new Vector3(-2.73f, 0.16f, pz + 0.04f), new Vector3(0.09f, 0.26f, 0.03f), bodyMat);
        Box(root, "Title", new Vector3(-2.8f, 1.00f, pz + 0.04f), new Vector3(0.82f, 0.07f, 0.02f), Mat(new Color(0.22f, 0.36f, 0.55f)));

        // Corner pins
        foreach (var off in new[] { new Vector3(-0.38f, 0.62f, 0), new Vector3(0.38f, 0.62f, 0), new Vector3(-0.38f, -0.62f, 0), new Vector3(0.38f, -0.62f, 0) })
            Sph(root, "Pin", new Vector3(-2.8f + off.x, 0.30f + off.y, pz + 0.03f), new Vector3(0.035f, 0.035f, 0.025f), Mat(new Color(0.80f, 0.10f, 0.10f)));

        // Frame
        Bar(root, new Vector3(-2.8f,  1.02f, pz + 0.015f), new Vector3(0.03f, 0.03f, 0.94f), new Color(0.12f, 0.08f, 0.04f));
        Bar(root, new Vector3(-2.8f, -0.42f, pz + 0.015f), new Vector3(0.03f, 0.03f, 0.94f), new Color(0.12f, 0.08f, 0.04f));
        Bar(root, new Vector3(-2.8f,  0.30f, pz + 0.015f + 0.47f), new Vector3(0.03f, 1.44f, 0.03f), new Color(0.12f, 0.08f, 0.04f));
        Bar(root, new Vector3(-2.8f,  0.30f, pz + 0.015f - 0.47f), new Vector3(0.03f, 1.44f, 0.03f), new Color(0.12f, 0.08f, 0.04f));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PICTURES  — 3 large Rothko-style paintings on right wall (quiz frames)
    // Large size makes images easy to read in VR. One row at eye level.
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildPictures()
    {
        Color[][] palettes =
        {
            new[]{ new Color(0.08f,0.06f,0.28f), new Color(0.14f,0.28f,0.62f), new Color(0.52f,0.72f,0.92f) },
            new[]{ new Color(0.06f,0.22f,0.10f), new Color(0.24f,0.50f,0.28f), new Color(0.70f,0.86f,0.72f) },
            new[]{ new Color(0.22f,0.10f,0.04f), new Color(0.68f,0.30f,0.08f), new Color(0.94f,0.76f,0.32f) },
        };

        float rx   = H - 0.06f;
        float picW = 1.60f, picH = 1.20f, ft = 0.07f;  // larger frames, easier to see in VR
        float rowY = 0.20f;                               // eye-level centre
        float[] colZ = { -2.2f, 0f, 2.2f };             // spread across wall

        // Dado rail
        var railRoot = new GameObject("DadoRail");
        railRoot.transform.SetParent(transform);
        Bar(railRoot, new Vector3(rx - ft, -0.06f, 0f),
            new Vector3(ft * 0.4f, 0.045f, H * 2f - 0.14f), new Color(0.50f, 0.38f, 0.18f));

        for (int idx = 0; idx < 3; idx++)
        {
            PlacePicture(idx, rx, rowY, colZ[idx], picW, picH, ft, palettes[idx]);
        }
    }

    /// <summary>
    /// Called by WallImageQuiz after each round to restore the Rothko paintings.
    /// </summary>
    public void RepaintPictures()
    {
        Color[][] palettes =
        {
            new[]{ new Color(0.08f,0.06f,0.28f), new Color(0.14f,0.28f,0.62f), new Color(0.52f,0.72f,0.92f) },
            new[]{ new Color(0.06f,0.22f,0.10f), new Color(0.24f,0.50f,0.28f), new Color(0.70f,0.86f,0.72f) },
            new[]{ new Color(0.22f,0.10f,0.04f), new Color(0.68f,0.30f,0.08f), new Color(0.94f,0.76f,0.32f) },
        };

        for (int i = 0; i < PictureCanvases.Length; i++)
        {
            var rend = PictureCanvases[i];
            if (rend == null) continue;
            var tex = GeneratePainting(256, 192, palettes[i % palettes.Length]);
            rend.material.mainTexture = tex;
            rend.material.SetTexture("_BaseMap", tex);
            rend.material.SetTexture("_MainTex",  tex);
            rend.material.SetColor("_BaseColor", Color.white);
            rend.material.SetColor("_Color",     Color.white);
            rend.material.color = Color.white;
        }
    }

    void PlacePicture(int idx, float rx, float py, float pz,
                      float w, float h, float ft, Color[] palette)
    {
        Color fCol = (idx % 2 == 0)
            ? new Color(0.10f, 0.06f, 0.02f)   // dark walnut
            : new Color(0.72f, 0.58f, 0.26f);  // antique gold

        var root = new GameObject($"Picture_{idx}");
        root.transform.SetParent(transform);

        // Frame strips
        float hw = w / 2f, hh = h / 2f;
        Bar(root, new Vector3(rx - ft * 0.5f, py + hh + ft * 0.5f, pz), new Vector3(ft, ft, w + ft * 2f), fCol);
        Bar(root, new Vector3(rx - ft * 0.5f, py - hh - ft * 0.5f, pz), new Vector3(ft, ft, w + ft * 2f), fCol);
        Bar(root, new Vector3(rx - ft * 0.5f, py, pz + hw + ft * 0.5f), new Vector3(ft, h + ft * 2f, ft), fCol);
        Bar(root, new Vector3(rx - ft * 0.5f, py, pz - hw - ft * 0.5f), new Vector3(ft, h + ft * 2f, ft), fCol);

        // Canvas quad facing -X (into room)
        var canvas = GameObject.CreatePrimitive(PrimitiveType.Quad);
        canvas.name = "Canvas";
        canvas.transform.SetParent(root.transform);
        canvas.transform.position    = new Vector3(rx - ft, py, pz);
        canvas.transform.localScale  = new Vector3(w, h, 1f);
        canvas.transform.eulerAngles = new Vector3(0, 90, 0);   // +90 → front face points -X (into room)
        Destroy(canvas.GetComponent<Collider>());

        // Generate Rothko-style procedural painting using the palette
        var tex = GeneratePainting(256, 192, palette);
        var mat = MatCull(Color.white);
        mat.mainTexture = tex;
        mat.SetTexture("_BaseMap", tex);   // URP
        mat.SetTexture("_MainTex", tex);   // Built-in
        var rend = canvas.GetComponent<MeshRenderer>();
        rend.material = mat;
        Debug.Log($"[DoctorRoomDecorator] Canvas {idx} set to Rothko painting");

        // Expose canvas renderer so WallImageQuiz can replace textures at runtime
        if (idx >= 0 && idx < PictureCanvases.Length)
            PictureCanvases[idx] = rend;
    }

    // ── Rothko-style procedural painting ─────────────────────────────────────
    static Texture2D GeneratePainting(int w, int h, Color[] stops)
    {
        var tex    = new Texture2D(w, h, TextureFormat.RGB24, false);
        var pixels = new Color[w * h];
        int n      = stops.Length;

        for (int y = 0; y < h; y++)
        {
            float t   = (float)y / (h - 1);
            // Band position
            float pos = t * (n - 1);
            int   s0  = Mathf.FloorToInt(pos);
            int   s1  = Mathf.Min(s0 + 1, n - 1);
            float bt  = pos - s0;
            // Smooth step
            bt = bt * bt * (3f - 2f * bt);
            Color row = Color.Lerp(stops[s0], stops[s1], bt);

            for (int x = 0; x < w; x++)
            {
                // Subtle horizontal variation — canvas texture feel
                float noise = (Mathf.Sin(x * 0.18f) * 0.5f + Mathf.Cos(y * 0.22f) * 0.5f) * 0.018f;
                pixels[y * w + x] = new Color(
                    Mathf.Clamp01(row.r + noise),
                    Mathf.Clamp01(row.g + noise),
                    Mathf.Clamp01(row.b + noise));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CHAIRS
    // ═══════════════════════════════════════════════════════════════════════════

    // Doctor's ergonomic chair — back panel on the +Z side (person faces -Z toward patient)
    void BuildDoctorChair(GameObject parent, Vector3 footPos)
    {
        float sh = 0.48f, sw = 0.55f;
        Color sC = new Color(0.09f, 0.09f, 0.09f);
        Color mC = new Color(0.32f, 0.32f, 0.32f);

        Box(parent, "DSeat",  footPos + V(0, sh + 0.03f, 0),           V(sw, 0.07f, sw),            Mat(sC));
        // Back on +Z side → person faces -Z
        Box(parent, "DBack",  footPos + V(0, sh + 0.46f, sw/2f - 0.04f), V(sw, 0.76f, 0.06f),      Mat(sC));
        Box(parent, "DArmL", footPos + V(-sw/2f+0.04f, sh+0.26f, 0),  V(0.05f, 0.40f, sw-0.08f), Mat(sC));
        Box(parent, "DArmR", footPos + V( sw/2f-0.04f, sh+0.26f, 0),  V(0.05f, 0.40f, sw-0.08f), Mat(sC));
        Cyl(parent, "DPole", footPos + V(0, sh / 2f, 0),               V(0.06f, sh/2f, 0.06f),     Mat(mC));

        for (int i = 0; i < 5; i++)
        {
            float a   = i * 72f * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            var leg   = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name  = "DLeg";
            leg.transform.SetParent(parent.transform);
            leg.transform.position   = footPos + d * 0.17f + V(0, 0.025f, 0);
            leg.transform.localScale = V(0.04f, 0.04f, 0.34f);
            leg.transform.rotation   = Quaternion.LookRotation(d);
            Destroy(leg.GetComponent<Collider>());
            leg.GetComponent<MeshRenderer>().material = Mat(mC);
        }
    }

    // Simple visitor chair — back on -Z side (person faces +Z toward desk)
    void BuildSimpleChair(GameObject parent, Vector3 footPos)
    {
        float sh = 0.46f, sw = 0.50f;
        Color sC = new Color(0.24f, 0.34f, 0.44f); // blue-grey
        Color lC = new Color(0.42f, 0.30f, 0.16f); // wood

        Box(parent, "Seat",  footPos + V(0, sh, 0),                    V(sw, 0.06f, sw),       Mat(sC));
        // Back on -Z side → person faces +Z (toward desk)
        Box(parent, "Back",  footPos + V(0, sh+0.38f, -sw/2f+0.03f),  V(sw, 0.62f, 0.05f),   Mat(sC));

        float lx = sw/2f - 0.04f, lz = sw/2f - 0.04f;
        foreach (var p in new[] { V(-lx,0,-lz), V(lx,0,-lz), V(-lx,0,lz), V(lx,0,lz) })
            Box(parent, "Leg", footPos + V(p.x, sh/2f, p.z), V(0.04f, sh, 0.04f), Mat(lC));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Primitive helpers
    // ═══════════════════════════════════════════════════════════════════════════

    static void Box(GameObject p, string n, Vector3 pos, Vector3 sc, Material m)
        => MkPrim(p, PrimitiveType.Cube, n, pos, sc, m);
    static void Cyl(GameObject p, string n, Vector3 pos, Vector3 sc, Material m)
        => MkPrim(p, PrimitiveType.Cylinder, n, pos, sc, m);
    static void Sph(GameObject p, string n, Vector3 pos, Vector3 sc, Material m)
        => MkPrim(p, PrimitiveType.Sphere, n, pos, sc, m);

    static void MkPrim(GameObject parent, PrimitiveType t, string n,
                       Vector3 pos, Vector3 sc, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = n;
        go.transform.SetParent(parent.transform);
        go.transform.position   = pos;
        go.transform.localScale = sc;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().material = m;
    }

    void Bar(GameObject parent, Vector3 worldPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Bar";
        go.transform.SetParent(parent.transform);
        go.transform.position   = worldPos;
        go.transform.localScale = scale;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().material = Mat(color);
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static Shader _sh;
    static Shader GetShader()
    {
        if (_sh == null)
            _sh = Shader.Find("Universal Render Pipeline/Unlit")
               ?? Shader.Find("Unlit/Color")
               ?? Shader.Find("Standard");
        return _sh;
    }

    static Material Mat(Color c)
    {
        var m = new Material(GetShader());
        m.SetColor("_BaseColor", c);
        m.SetColor("_Color",     c);
        return m;
    }

    // Like Mat() but with back-face culling disabled — for canvas quads
    // that might be viewed from either side.
    static Material MatCull(Color c)
    {
        var m = Mat(c);
        m.SetInt("_Cull", 0);          // 0 = Off (both sides rendered)
        m.SetInt("_CullMode", 0);      // legacy alias
        return m;
    }

    static Material Wood()   => Mat(new Color(0.40f, 0.26f, 0.13f));
    static Material DkWood() => Mat(new Color(0.24f, 0.14f, 0.07f));
}
