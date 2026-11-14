"""
TWO WORLDS: procedural model generator (2-day jam prototype).

Run (from the repo root):
    blender -b -P Tools/Blender/build_twoworlds_models.py -- [--preview] [--only Name1,Name2]

Writes one FBX per model into Assets/TwoWorlds/Models/ (Y-up, forward -Z, 1 unit = 1 metre).
Optional --preview renders a PNG per model into Tools/Blender/previews_twoworlds/.
Models face -Y in Blender (+Z in Unity after export). Terrains are 60 x 60 m, centred on the origin;
in Blender the second ground axis is Y (it becomes Unity Z).
"""
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "TwoWorlds", "Models")
PREV = os.path.join(HERE, "previews_twoworlds")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = "--preview" in ARGS
LOWPOLY = "--smooth" not in ARGS   # default: flat-colour low-poly, matching the design document
ONLY = set(ARGS[ARGS.index("--only") + 1].split(",")) if "--only" in ARGS else None

C = {}

# ------------------------------------------------------------------ palette
C = dict(
    stone=(0.52, 0.48, 0.40), stone_dark=(0.30, 0.28, 0.25),
    gold=(1.0, 0.78, 0.30), crystal=(1.0, 0.90, 0.65), pale=(0.85, 0.88, 0.95),
    shadow=(0.03, 0.03, 0.04), ash=(0.35, 0.35, 0.38),
    moss=(0.25, 0.50, 0.20), leaf=(0.45, 0.65, 0.20), bark=(0.35, 0.25, 0.15), bark_dead=(0.12, 0.12, 0.14),
    thorn=(0.18, 0.30, 0.12), root_live=(0.30, 0.22, 0.12), root_dead=(0.20, 0.20, 0.22),
)

_mats = {}
def mat(name, rgb, rough=0.8, emit=0.0, emit_rgb=None, metallic=0.0):
    if name in _mats and name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    n = m.node_tree.nodes.get("Principled BSDF")
    n.inputs["Base Color"].default_value = (*rgb, 1)
    n.inputs["Roughness"].default_value = rough
    n.inputs["Metallic"].default_value = metallic
    if emit > 0:
        n.inputs["Emission Color"].default_value = (*(emit_rgb or rgb), 1)
        n.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*rgb, 1)
    _mats[name] = m
    return m

# named materials: Unity swaps these per world (Kay / Ayan) where needed
def M_Stone(): return mat("M_RuinStone", C["stone"])
def M_StoneDark(): return mat("M_RuinStoneDark", C["stone_dark"])
def M_SunGlow(): return mat("M_SunGlow", C["gold"], 0.3, 3.0)
def M_PaleGlow(): return mat("M_PaleGlow", C["pale"], 0.3, 3.0)
def M_KayCrystal(): return mat("M_KayCrystal", C["crystal"], 0.1, 0.8, C["gold"])
def M_AyanShadow(): return mat("M_AyanShadow", C["shadow"], 0.9)
def M_Leaf(): return mat("M_Leaf", C["leaf"], 0.7)
def M_Moss(): return mat("M_Moss", C["moss"], 0.9)
def M_Bark(): return mat("M_Bark", C["bark"], 0.9)
def M_BarkDead(): return mat("M_BarkDead", C["bark_dead"], 0.9)
def M_Ash(): return mat("M_Ash", C["ash"], 1.0)
def M_Thorn(): return mat("M_Thorn", C["thorn"], 0.8)
def M_RootLive(): return mat("M_RootLive", C["root_live"], 0.9)
def M_RootDead(): return mat("M_RootDead", C["root_dead"], 0.95)

# ------------------------------------------------------------------ helpers
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()

def _finish(o, name, material, parent, loc, rot, scale):
    o.name = name
    if o.data: o.data.name = name
    o.rotation_euler = rot
    o.location = loc
    if scale is not None and tuple(scale) != (1, 1, 1):
        o.data.transform(Matrix.Diagonal((*scale, 1)))
    if material is not None:
        o.data.materials.clear(); o.data.materials.append(material)
    if parent is not None: o.parent = parent
    return o

def cube(name, size, loc=(0, 0, 0), rot=(0, 0, 0), material=None, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1)
    return _finish(bpy.context.object, name, material, parent, loc, rot, size)

def cyl(name, r, h, loc=(0, 0, 0), rot=(0, 0, 0), verts=8, r2=None, material=None, parent=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=h)
    return _finish(bpy.context.object, name, material, parent, loc, rot, None)

def ico(name, r, loc=(0, 0, 0), sub=1, scale=None, material=None, parent=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r)
    return _finish(bpy.context.object, name, material, parent, loc, rot, scale)

def uvs(name, r, loc=(0, 0, 0), seg=16, rings=10, scale=None, material=None, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r)
    o = _finish(bpy.context.object, name, material, parent, loc, (0, 0, 0), scale)
    bpy.ops.object.shade_smooth()
    return o

def torus(name, R, r, loc=(0, 0, 0), rot=(0, 0, 0), maj=24, mnr=8, material=None, parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=maj, minor_segments=mnr)
    return _finish(bpy.context.object, name, material, parent, loc, rot, None)

def jitter(o, amt, seed=1):
    rnd = random.Random(seed)
    for v in o.data.vertices:
        v.co += Vector((rnd.uniform(-amt, amt), rnd.uniform(-amt, amt), rnd.uniform(-amt, amt)))

def empty(name, loc=(0, 0, 0)):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.location = loc
    return o

def poly(name, pts, thick, loc=(0, 0, 0), rot=(0, 0, 0), material=None, parent=None):
    """Extruded 2D polygon (pts in XY, extruded along +Z by thick)."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, 0)) for x, y in pts]
    f = bm.faces.new(vs)
    ret = bmesh.ops.extrude_face_region(bm, geom=[f])
    vv = [e for e in ret["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, 0, thick), verts=vv)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = loc; o.rotation_euler = rot
    if material is not None: me.materials.append(material)
    if parent is not None: o.parent = parent
    return o

# ------------------------------------------------------------------ models
MODELS = {}
def model(fn):
    MODELS[fn.__name__] = fn
    return fn


# ================================================================== TWO WORLDS materials
def T(name, rgb, rough=0.9, emit=0.0, emit_rgb=None, metallic=0.0):
    return mat(name, rgb, rough, emit, emit_rgb, metallic)

# Desert (Present): pale, faded
def D_Sand(): return T("M_DesertSand", (0.78, 0.70, 0.56), 1.0)
def D_WetSand(): return T("M_WetSand", (0.45, 0.38, 0.30), 1.0)
def D_Rock(): return T("M_DesertRock", (0.55, 0.50, 0.45), 1.0)
def D_Wood(): return T("M_DryWood", (0.48, 0.42, 0.34), 1.0)
def D_Ruin(): return T("M_DesertStone", (0.62, 0.58, 0.52), 1.0)
# Forest (Future): lush
def F_Grass(): return T("M_ForestGround", (0.22, 0.45, 0.18), 1.0)
def F_Mud(): return T("M_StreamBed", (0.25, 0.20, 0.14), 1.0)
def F_Rock(): return T("M_MossRock", (0.35, 0.42, 0.32), 1.0)
def F_Bark(): return T("M_ForestBark", (0.30, 0.20, 0.12), 0.9)
def F_Leaf(): return T("M_ForestLeaf", (0.20, 0.55, 0.18), 0.7)
def F_LeafLight(): return T("M_ForestLeafLight", (0.45, 0.72, 0.25), 0.7)
def F_Glow(): return T("M_ForestGlow", (0.4, 1.0, 0.7), 0.3, 3.0)
def F_Root(): return T("M_MagicRoot", (0.35, 0.28, 0.14), 0.8, 0.4, (0.4, 0.9, 0.4))
# Shared
def S_Gold(): return T("M_GoldGlow", (1.0, 0.82, 0.35), 0.3, 3.0)
def S_Mirror(): return T("M_MirrorGlass", (0.8, 0.9, 1.0), 0.05, 0.3, (0.7, 0.85, 1.0), 0.9)
def S_Frame(): return T("M_MirrorFrame", (0.45, 0.32, 0.15), 0.6, 0.0, None, 0.5)
def S_Water(): return T("M_RiverWater", (0.25, 0.55, 0.85), 0.05, 0.6, (0.3, 0.6, 1.0))
def S_Floor(): return T("M_RoomFloor", (0.35, 0.30, 0.28), 0.9)
def S_Wall(): return T("M_RoomWall", (0.55, 0.52, 0.50), 1.0)

_ico_full, _uvs_full = ico, uvs
def ico(name, r, loc=(0, 0, 0), sub=1, scale=None, material=None, parent=None, rot=(0, 0, 0)):
    return _ico_full(name, r, loc, min(sub, 1) if LOWPOLY else sub, scale, material, parent, rot)
def uvs(name, r, loc=(0, 0, 0), seg=16, rings=10, scale=None, material=None, parent=None):
    return _uvs_full(name, r, loc, min(seg, 10) if LOWPOLY else seg, min(rings, 6) if LOWPOLY else rings, scale, material, parent)

# ------------------------------------------------------------------ realism helpers
def _smooth(o):
    if LOWPOLY: return
    try:
        o.data.polygons.foreach_set("use_smooth", [True] * len(o.data.polygons))
    except Exception:
        pass

_ntex = []
def _noise(scale):
    t = bpy.data.textures.new("Noise%d" % len(_ntex), 'CLOUDS')
    t.noise_scale = scale; t.noise_depth = 2
    _ntex.append(t)
    return t

def shape(o, bevel=0.0, sub=0, disp=0.0, scale=0.5, smooth=True):
    """Soften and add natural surface detail through modifiers (applied on export). No-op in low-poly mode."""
    if LOWPOLY: return o
    if bevel:
        m = o.modifiers.new("Bevel", 'BEVEL'); m.width = bevel; m.segments = 3; m.limit_method = 'ANGLE'
    if sub:
        m = o.modifiers.new("Subsurf", 'SUBSURF'); m.levels = sub; m.render_levels = sub
    if disp:
        m = o.modifiers.new("Disp", 'DISPLACE'); m.texture = _noise(scale); m.strength = disp; m.mid_level = 0.5
    if smooth: _smooth(o)
    return o

def boulder(name, r, loc, seed, material, parent, squash=(1.0, 0.9, 0.7), rough=0.3):
    o = ico(name, r, loc, 3, squash, material, parent)
    jitter(o, r * 0.06, seed)
    shape(o, disp=r * rough, scale=r * 1.1)
    return o

def vnoise_h(i, j):
    s = math.sin(i * 127.1 + j * 311.7) * 43758.5453
    return s - math.floor(s)

def vnoise(x, y):
    xi, yi = math.floor(x), math.floor(y)
    xf, yf = x - xi, y - yi
    u = xf * xf * (3 - 2 * xf); v = yf * yf * (3 - 2 * yf)
    a = vnoise_h(xi, yi); b = vnoise_h(xi + 1, yi); c = vnoise_h(xi, yi + 1); d = vnoise_h(xi + 1, yi + 1)
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v

def fbm(x, y, octaves=4):
    s, a, f = 0.0, 0.5, 1.0
    for _ in range(octaves):
        s += a * vnoise(x * f, y * f); f *= 2.0; a *= 0.5
    return s

def _blend(t): return t * t * (3 - 2 * t)
def _clamp01(t): return max(0.0, min(1.0, t))

# ================================================================== TERRAIN
CH_Y0, CH_Y1, CH_X = 14, 19, 27       # chasm / gap, same place in both worlds
RIVER_X = 20                          # Memory River runs north-south along x = 20

def stream_y(x):                      # forest stream centre line, from the spring at (-26,-10) to the river
    return -10.0 + 10.0 * (x + 26.0) / 46.0 + 2.0 * math.sin(x * 0.2)

def in_chasm(x, y):
    e = 0.5 * (fbm(x * 0.25, 7.0, 2) - 0.5) * 2
    return (CH_Y0 + e) <= y <= (CH_Y1 + e) and abs(x) <= CH_X

def desert_h(x, y):
    r = max(abs(x), abs(y))
    w = fbm(x * 0.05, y * 0.05, 3)
    crest = 1.0 - abs(math.sin(x * 0.11 + y * 0.05 + w * 4.0))
    play = 0.30 + 0.70 * _blend(_clamp01((r - 16) / 10))        # calmer ground where the players walk
    h = (1.3 * crest * crest + 0.9 * fbm(x * 0.08 + 5, y * 0.08, 3) - 0.9) * play
    h += 0.045 * math.sin(x * 2.3 + w * 18.0) * play             # wind ripples
    h += 7.0 * _blend(_clamp01((r - 22 + 3 * (fbm(x * 0.1, y * 0.1, 2) - 0.5)) / 8))
    if abs(y) < 28: h -= 1.3 * math.exp(-((x - RIVER_X) / 2.4) ** 2)
    if in_chasm(x, y): h = -6.0 + 1.6 * fbm(x * 0.5, y * 0.5, 3)
    return h

def forest_h(x, y):
    r = max(abs(x), abs(y))
    h = 0.55 * fbm(x * 0.07, y * 0.07, 4) - 0.2 + 0.04 * math.sin(x * 1.7 + y * 1.3)
    h += 8.0 * _blend(_clamp01((r - 22 + 3 * (fbm(x * 0.1, y * 0.1, 2) - 0.5)) / 8))
    if abs(y) < 28: h -= 1.1 * math.exp(-((x - RIVER_X) / 2.4) ** 2)
    if x < 21: h -= 0.9 * math.exp(-((y - stream_y(x)) / 1.4) ** 2)
    if in_chasm(x, y): h = -6.0 + 1.6 * fbm(x * 0.5, y * 0.5, 3)
    return h

def make_terrain(name, fn, mats, picker, uvscale=8.0):
    N, step = (60, 1.0) if LOWPOLY else (120, 0.5)
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = []
    for j in range(N + 1):
        row = []
        for i in range(N + 1):
            x, y = (i - N // 2) * step, (j - N // 2) * step
            row.append(bm.verts.new((x, y, fn(x, y))))
        grid.append(row)
    for j in range(N):
        for i in range(N):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            cz = sum(v.co.z for v in f.verts) / 4
            cx = sum(v.co.x for v in f.verts) / 4
            cy = sum(v.co.y for v in f.verts) / 4
            f.material_index = picker(cx, cy, cz)
            for l in f.loops:
                l[uv].uv = (l.vert.co.x / uvscale, l.vert.co.y / uvscale)
    bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    _smooth(o)
    return o

@model
def DesertTerrain():
    def pick(x, y, z):
        if z < -2.5: return 2
        if abs(x - RIVER_X) < 3.2 and abs(y) < 28 and z < -0.5: return 1
        return 0
    make_terrain("DesertTerrain", desert_h, [D_Sand(), D_WetSand(), D_Rock()], pick)

@model
def ForestTerrain():
    def pick(x, y, z):
        if z < -2.5: return 2
        if abs(x - RIVER_X) < 3.2 and abs(y) < 28 and z < -0.4: return 1
        if x < 21 and abs(y - stream_y(x)) < 1.8 and z < -0.35: return 1
        return 0
    make_terrain("ForestTerrain", forest_h, [F_Grass(), F_Mud(), F_Rock()], pick)

def strip(name, pts, width, z, material, waves=0.0):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    prev = None
    run = 0.0
    for k, (x, y) in enumerate(pts):
        if k < len(pts) - 1:
            dx, dy = pts[k + 1][0] - x, pts[k + 1][1] - y
        L = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / L, dx / L
        zz = z + waves * math.sin(k * 1.7)
        a = bm.verts.new((x + nx * width / 2, y + ny * width / 2, zz))
        b = bm.verts.new((x - nx * width / 2, y - ny * width / 2, zz))
        if prev:
            f = bm.faces.new((prev[0], prev[1], b, a))
            f.loops[0][uv].uv = (0, run - L); f.loops[1][uv].uv = (1, run - L)
            f.loops[2][uv].uv = (1, run); f.loops[3][uv].uv = (0, run)
        prev = (a, b); run += L * 0.25
    bm.to_mesh(me); bm.free()
    me.materials.append(material)
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    _smooth(o)
    return o

@model
def RiverWater():
    strip("RiverWater", [(RIVER_X, y * 0.5) for y in range(-56, 57)], 4.4, -0.35, S_Water(), 0.01)

@model
def StreamWater():
    strip("StreamWater", [(x * 0.5, stream_y(x * 0.5)) for x in range(-52, 43)], 1.8, -0.3, S_Water(), 0.008)

# ================================================================== CHARACTERS (same human, two states)
def tube(name, r_bottom, r_top, length, loc, material, parent, rot=(0, 0, 0), verts=12):
    o = cyl(name, r_bottom, length, loc, rot, verts, r2=r_top, material=material, parent=parent)
    shape(o, bevel=min(r_bottom, r_top) * 0.5, sub=1)
    return o

def _human(name, present):
    root = empty(name)
    sfx = "Present" if present else "Future"
    skin = T("M_Skin_" + sfx, (0.78, 0.60, 0.48) if not present else (0.64, 0.54, 0.47), 0.6)
    shirt = T("M_Shirt_" + sfx, (0.42, 0.42, 0.45) if present else (0.93, 0.52, 0.22), 0.9)
    pants = T("M_Pants_" + sfx, (0.33, 0.31, 0.29) if present else (0.18, 0.33, 0.62), 0.9)
    hair = T("M_Hair_" + sfx, (0.17, 0.13, 0.11), 0.7)
    shoe = T("M_Shoes_" + sfx, (0.20, 0.20, 0.20) if present else (0.93, 0.88, 0.82), 0.7)
    white = T("M_EyeWhite", (0.92, 0.92, 0.9), 0.3)
    iris = T("M_Eyes", (0.06, 0.04, 0.03), 0.2)
    bags = T("M_EyeBags", (0.30, 0.22, 0.22), 1.0)

    lean = math.radians(20 if present else 2)
    headlean = math.radians(24 if present else -2)
    arm_fwd = math.radians(-14 if present else 4)
    sh_z = 0.54 if present else 0.57
    sh_x = 0.20 if present else 0.215

    # legs
    for s, n in ((-1, "L"), (1, "R")):
        hj = empty("Hip_" + n, (s * 0.095, 0, 0.90)); hj.parent = root
        tube("UpperLeg_" + n, 0.065, 0.088, 0.51, (0, 0, -0.22), pants, hj)
        kj = empty("Knee_" + n, (0, 0, -0.45)); kj.parent = hj
        tube("LowerLeg_" + n, 0.045, 0.066, 0.48, (0, 0, -0.205), pants, kj)
        f = cube("Foot_" + n, (0.105, 0.27, 0.085), (0, -0.04, -0.455), material=shoe, parent=kj)
        for v in f.data.vertices:                                  # taper the toe
            if v.co.y < 0: v.co.x *= 0.78; v.co.z *= 0.8
        shape(f, bevel=0.025, sub=1)
    # spine and torso
    sp = empty("Spine", (0, 0, 0.92)); sp.parent = root
    sp.rotation_euler = (lean, 0, 0)
    t = cube("Torso", (0.34, 0.2, 0.58), (0, 0, 0.30), material=shirt, parent=sp)
    for v in t.data.vertices:
        if v.co.z > 0: v.co.x *= 1.13; v.co.y *= 1.0
        else: v.co.x *= 0.9; v.co.y *= 0.92
    shape(t, bevel=0.05, sub=1)
    b = cube("Belt", (0.32, 0.19, 0.06), (0, 0, 0.02), material=pants, parent=sp); shape(b, bevel=0.015, sub=1)
    col = torus("Collar", 0.075, 0.018, (0, 0, 0.58), (0, 0, 0), 14, 6, shirt, sp); shape(col)
    nk = empty("Neck", (0, 0, 0.60)); nk.parent = sp
    nk.rotation_euler = (headlean, 0, 0)
    tube("NeckMesh", 0.05, 0.047, 0.1, (0, 0, 0.05), skin, nk, verts=10)
    # head
    h = ico("Head", 0.115, (0, 0, 0.2), 3, (0.86, 0.98, 1.1), skin, nk); shape(h)
    ch = ico("Chin", 0.06, (0, -0.045, 0.115), 2, (0.9, 0.9, 0.8), skin, nk); shape(ch)
    nz = cyl("Nose", 0.017, 0.05, (0, -0.108, 0.195), (math.radians(100), 0, 0), 5, r2=0.008, material=skin, parent=nk); shape(nz, sub=1)
    for s, n in ((-1, "L"), (1, "R")):
        e = ico("Ear_" + n, 0.028, (s * 0.103, 0.0, 0.2), 1, (0.4, 0.9, 1.2), skin, nk); shape(e, sub=1)
        ew = ico("EyeWhite_" + n, 0.016, (s * 0.04, -0.098, 0.22), 2, None, white, nk); shape(ew)
        ir = ico("Iris_" + n, 0.009, (s * 0.04, -0.111, 0.22), 1, None, iris, nk); shape(ir)
        br = cube("Brow_" + n, (0.05, 0.014, 0.012), (s * 0.042, -0.103, 0.248), (0, 0, s * math.radians(-6 if present else 6)), hair, nk); shape(br, bevel=0.004, sub=1)
        if present:
            eb = ico("EyeBag_" + n, 0.02, (s * 0.04, -0.097, 0.197), 2, (1.2, 0.25, 0.5), bags, nk); shape(eb)
    mo = cube("Mouth", (0.04, 0.01, 0.007), (0, -0.106, 0.145 if present else 0.15), (0, 0, 0 if present else 0), iris, nk); shape(mo, bevel=0.002, sub=1)
    if present:     # messy, heavy hair that falls over the forehead
        for i, (x, y, z, sz) in enumerate(((0, 0.01, 0.285, 0.11), (-0.06, -0.03, 0.27, 0.07), (0.06, -0.03, 0.27, 0.07), (0, -0.07, 0.27, 0.06), (0, 0.06, 0.2, 0.09))):
            hb = ico("Hair%d" % i, sz, (x, y, z), 2, (1, 1, 0.8), hair, nk); jitter(hb, 0.008, i); shape(hb)
    else:           # tidy swept hair
        hb = ico("Hair", 0.122, (0, 0.012, 0.222), 3, (0.93, 1.0, 0.85), hair, nk); shape(hb)
        sw = ico("HairSweep", 0.06, (0.02, -0.07, 0.29), 2, (1.4, 0.8, 0.6), hair, nk); shape(sw)
    # arms
    for s, n in ((-1, "L"), (1, "R")):
        sj = empty("Shoulder_" + n, (s * sh_x, 0, sh_z)); sj.parent = sp
        sj.rotation_euler = (arm_fwd, 0, s * math.radians(4 if present else 9))
        tube("UpperArm_" + n, 0.04, 0.052, 0.36, (0, 0, -0.15), shirt, sj, verts=10)
        ej = empty("Elbow_" + n, (0, 0, -0.30)); ej.parent = sj
        ej.rotation_euler = (math.radians(-10 if present else -6), 0, 0)
        tube("LowerArm_" + n, 0.034, 0.042, 0.33, (0, 0, -0.135), skin, ej, verts=10)
        hd = ico("Hand_" + n, 0.045, (0, 0, -0.31), 2, (0.55, 0.9, 1.0), skin, ej); shape(hd)
        th = ico("Thumb_" + n, 0.017, (s * -0.03, -0.01, -0.29), 1, (1, 1, 1.9), skin, ej); shape(th, sub=1)
    if not present:
        ico("Aura", 0.04, (0, 0, 1.2), 1, None, S_Gold(), root)
        empty("LightPoint", (0, 0, 1.3)).parent = root

@model
def Human_Present(): _human("Human_Present", True)

@model
def Human_Future(): _human("Human_Future", False)

# ================================================================== DESERT PROPS
def _mound(name, rx, ry, rz, seed):
    r = empty(name)
    o = uvs("Mound", 1.0, (0, 0, 0), 28, 16, (rx, ry, rz), D_Sand(), r)
    for v in o.data.vertices:
        if v.co.z < 0: v.co.z = 0
    shape(o, disp=0.06, scale=0.35)
    o2 = uvs("Wind", 1.0, (0.2, 0.1, 0), 20, 10, (rx * 0.6, ry * 0.5, rz * 0.7), D_Sand(), r)   # layered wind-shaped second lobe
    for v in o2.data.vertices:
        if v.co.z < 0: v.co.z = 0
    shape(o2, disp=0.04, scale=0.3)

@model
def SandMound_Small(): _mound("SandMound_Small", 1.7, 1.5, 0.75, 2)

@model
def SandMound_Large(): _mound("SandMound_Large", 2.8, 2.5, 1.25, 4)

@model
def Rock_Desert():
    r = empty("Rock_Desert")
    boulder("Rock0", 0.9, (0, 0, 0.45), 1, D_Rock(), r)
    boulder("Rock1", 0.5, (1.0, 0.5, 0.22), 2, D_Rock(), r)
    boulder("Rock2", 0.38, (-0.8, 0.6, 0.18), 3, D_Rock(), r)
    boulder("Rock3", 0.22, (0.4, -0.9, 0.1), 4, D_Rock(), r)

def _board(name, size, loc, rot, material, parent, dent=0.01):
    o = cube(name, size, loc, rot, material, parent)
    shape(o, bevel=0.01, sub=1, disp=dent, scale=0.15)
    return o

@model
def Chair_Buried():
    r = empty("Chair_Buried"); r.rotation_euler = (0.35, 0, 0.4)
    m = D_Wood()
    _board("Seat", (0.7, 0.7, 0.07), (0, 0, 0.4), (0, 0, 0), m, r)
    _board("Back", (0.7, 0.06, 0.7), (0, 0.33, 0.78), (-0.08, 0, 0), m, r)
    for k in range(4):
        _board("Slat%d" % k, (0.08, 0.04, 0.5), (-0.26 + k * 0.17, 0.33, 0.8), (-0.08, 0, 0), m, r)
    for sx in (-1, 1):
        for sy in (-1, 1):
            tube("Leg", 0.032, 0.036, 0.46, (sx * 0.3, sy * 0.3, 0.2), m, r, verts=8)

@model
def PictureFrame_Broken():
    r = empty("PictureFrame_Broken"); r.rotation_euler = (-0.5, 0, 0.3)
    m = S_Frame()
    _board("FrameTop", (0.8, 0.05, 0.08), (0, 0, 0.9), (0, 0, 0), m, r, 0.004)
    _board("FrameLeft", (0.08, 0.05, 0.9), (-0.36, 0, 0.5), (0, 0, 0), m, r, 0.004)
    _board("FrameRight", (0.08, 0.05, 0.5), (0.36, 0, 0.3), (0, 0, 0.03), m, r, 0.004)
    _board("Canvas", (0.62, 0.015, 0.6), (0, 0.01, 0.55), (0, 0, 0), D_Ruin(), r, 0.004)
    for k, (x, z) in enumerate(((0.2, 0.7), (-0.15, 0.35))):
        poly("Glass%d" % k, [(0, 0), (0.18, 0.04), (0.08, 0.16)], 0.006, (x, -0.02, z), (math.pi / 2, 0, 0), S_Mirror(), r)

@model
def Pot_Broken():
    r = empty("Pot_Broken")
    o = cyl("Pot", 0.34, 0.55, (0, 0, 0.28), verts=20, r2=0.26, material=D_Ruin(), parent=r)
    for v in o.data.vertices:
        if v.co.z > 0 and v.co.x > 0.05: v.co.z -= 0.22 + 0.06 * math.sin(v.co.y * 20)
    shape(o, bevel=0.015, sub=1, disp=0.012, scale=0.1)
    for k in range(3):
        s = cyl("Shard%d" % k, 0.12 + 0.03 * k, 0.03, (0.45 + 0.2 * k, 0.15 * k, 0.03), (0.2 * k, 0.1, k), 5, material=D_Ruin(), parent=r); shape(s, bevel=0.008, sub=1)

@model
def DeadTree_Desert():
    r = empty("DeadTree_Desert")
    tr = cyl("Trunk", 0.38, 3.8, (0, 0, 1.9), verts=12, r2=0.14, material=D_Wood(), parent=r)
    shape(tr, sub=1, disp=0.06, scale=0.35)
    for i in range(7):
        a = i * 0.9 + 0.4
        br = cyl("Branch%d" % i, 0.13, 1.7 - 0.1 * i, (math.cos(a) * 0.55, math.sin(a) * 0.55, 2.3 + i * 0.3),
                 (math.sin(a) * (0.8 + 0.05 * i), -math.cos(a) * (0.8 + 0.05 * i), 0), 7, r2=0.02, material=D_Wood(), parent=r)
        shape(br, sub=1, disp=0.03, scale=0.25)
    for i in range(5):
        a = i * 1.25
        rt = cyl("Root%d" % i, 0.2, 1.1, (math.cos(a) * 0.45, math.sin(a) * 0.45, 0.2), (math.sin(a) * 1.2, -math.cos(a) * 1.2, 0), 6, r2=0.04, material=D_Wood(), parent=r)
        shape(rt, sub=1)

@model
def DryShrub():
    r = empty("DryShrub")
    rnd = random.Random(14)
    for i in range(16):
        a = rnd.uniform(0, 6.28)
        c = cyl("Twig%d" % i, 0.02, 0.8 + rnd.uniform(0, 0.4), (math.cos(a) * 0.12, math.sin(a) * 0.12, 0.35), (math.sin(a) * rnd.uniform(0.5, 1.0), -math.cos(a) * rnd.uniform(0.5, 1.0), 0), 4, r2=0.004, material=D_Wood(), parent=r)

def _stone(o, chip=0.015, bevel=0.03):
    shape(o, bevel=bevel, sub=1, disp=chip, scale=0.3)

@model
def AncientPath():
    r = empty("AncientPath")
    for i in range(6):
        s = cube("PathStone%d" % i, (2.0, 1.5, 0.22), (0, i * 1.8, 0.11), (0, 0, (-1) ** i * 0.04), D_Ruin() if i % 2 else D_Rock(), r)
        _stone(s, 0.02)
        if i % 2 == 0:
            torus("Rune%d" % i, 0.5, 0.03, (0, i * 1.8, 0.23), (0, 0, 0), 24, 5, S_Gold(), r)

@model
def AncientBridge():
    r = empty("AncientBridge")
    for i in range(7):
        s = cube("Plank%d" % i, (3.0, 0.96, 0.25), (0, i * 1.0, 0.0), material=D_Ruin() if i % 2 else D_Rock(), parent=r); _stone(s, 0.012)
    for s in (-1, 1):
        rl = cube("Rail%s" % ("L" if s < 0 else "R"), (0.3, 7.0, 0.4), (s * 1.6, 3.0, 0.3), material=D_Ruin(), parent=r); _stone(rl, 0.02)
    for y in (-0.4, 6.4):
        e = cube("End%d" % (1 if y < 0 else 2), (3.4, 0.6, 0.6), (0, y, -0.15), material=D_Ruin(), parent=r); _stone(e, 0.02)

@model
def RiverStone():
    r = empty("RiverStone")
    boulder("Stone", 0.55, (0, 0, 0.55), 1, D_Ruin(), r, (0.8, 0.8, 1.3), 0.15)
    torus("Ring", 0.42, 0.04, (0, -0.44, 0.75), (math.pi / 2, 0, 0), 24, 6, S_Water(), r)
    b = cyl("Base", 0.7, 0.14, (0, 0, 0.07), verts=14, material=D_Rock(), parent=r); _stone(b)

@model
def FragmentShrine():
    r = empty("FragmentShrine")
    b = cyl("Base", 1.8, 0.4, (0, 0, 0.2), verts=24, material=D_Ruin(), parent=r); _stone(b, 0.02)
    a = cyl("Altar", 0.9, 0.9, (0, 0, 0.85), verts=16, r2=0.7, material=D_Rock(), parent=r); _stone(a, 0.02)
    t = cube("Top", (1.5, 1.5, 0.15), (0, 0, 1.38), material=D_Ruin(), parent=r); _stone(t, 0.01)
    for i in range(5):
        ang = math.radians(-70 + i * 35)
        s = cube("Slot%d" % (i + 1), (0.12, 0.05, 0.28), (math.sin(ang) * 1.35, -math.cos(ang) * 1.35, 0.7), (0, 0, ang), S_Gold() if i == 0 else D_Rock(), r)
        shape(s, bevel=0.01, sub=1)

# ================================================================== FOREST PROPS
def _crown(r, count, center, spread, rsize, seed):
    rnd = random.Random(seed)
    for i in range(count):
        p = (center[0] + rnd.uniform(-spread, spread), center[1] + rnd.uniform(-spread, spread), center[2] + rnd.uniform(-spread * 0.5, spread * 0.8))
        o = ico("Leaves%d" % i, rnd.uniform(*rsize), p, 3, (1, 1, 0.85), F_Leaf() if i % 3 else F_LeafLight(), r)
        jitter(o, 0.05, seed + i)
        shape(o, disp=0.28, scale=0.55)

@model
def Tree_Tall():
    r = empty("Tree_Tall")
    tr = cyl("Trunk", 0.62, 6.8, (0, 0, 3.4), verts=14, r2=0.28, material=F_Bark(), parent=r)
    shape(tr, sub=1, disp=0.07, scale=0.4)
    for i in range(7):
        a = i * 2 * math.pi / 7
        rt = cyl("Root%d" % i, 0.26, 1.7, (math.cos(a) * 0.6, math.sin(a) * 0.6, 0.25), (math.sin(a) * 1.05, -math.cos(a) * 1.05, 0), 7, r2=0.05, material=F_Bark(), parent=r)
        shape(rt, sub=1, disp=0.04, scale=0.3)
    for i in range(4):
        a = i * 1.7 + 0.5
        br = cyl("Branch%d" % i, 0.16, 2.6, (math.cos(a) * 0.9, math.sin(a) * 0.9, 5.2 + i * 0.3), (math.sin(a) * 0.9, -math.cos(a) * 0.9, 0), 7, r2=0.04, material=F_Bark(), parent=r)
        shape(br, sub=1, disp=0.03, scale=0.3)
    _crown(r, 30, (0, 0, 7.0), 2.1, (0.6, 1.25), 3)
    rnd = random.Random(8)
    for i in range(8):
        g = ico("Glow%d" % i, 0.1, (rnd.uniform(-1.8, 1.8), rnd.uniform(-1.8, 1.8), 6.0 + rnd.uniform(0, 2.6)), 2, None, F_Glow(), r); shape(g)

@model
def Tree_Round():
    r = empty("Tree_Round")
    tr = cyl("Trunk", 0.5, 3.8, (0, 0, 1.9), verts=14, r2=0.28, material=F_Bark(), parent=r)
    shape(tr, sub=1, disp=0.06, scale=0.35)
    for i in range(5):
        a = i * 1.3
        rt = cyl("Root%d" % i, 0.22, 1.3, (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.2), (math.sin(a) * 1.1, -math.cos(a) * 1.1, 0), 6, r2=0.05, material=F_Bark(), parent=r)
        shape(rt, sub=1)
    _crown(r, 22, (0, 0, 4.3), 1.6, (0.55, 1.1), 21)

@model
def Bush():
    r = empty("Bush")
    rnd = random.Random(5)
    for i in range(7):
        o = ico("Foliage%d" % i, rnd.uniform(0.35, 0.6), (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), 0.35 + rnd.uniform(0, 0.2)), 3, (1, 1, 0.8), F_Leaf() if i % 2 else F_LeafLight(), r)
        shape(o, disp=0.14, scale=0.3)

@model
def Fern():
    r = empty("Fern")
    for i in range(11):
        a = i * 2 * math.pi / 11 + 0.2
        for k in range(4):
            seg = cyl("Frond%d_%d" % (i, k), 0.05 - 0.01 * k, 0.28, (math.cos(a) * (0.12 + 0.2 * k), math.sin(a) * (0.12 + 0.2 * k), 0.15 + 0.22 * k - 0.04 * k * k),
                      (math.sin(a) * (0.9 + 0.2 * k), -math.cos(a) * (0.9 + 0.2 * k), 0), 4, r2=0.004, material=F_Leaf(), parent=r)

@model
def GrassTuft():
    r = empty("GrassTuft")
    rnd = random.Random(31)
    for i in range(30):
        a = rnd.uniform(0, 6.28); d = rnd.uniform(0, 0.15); h = rnd.uniform(0.3, 0.65)
        cyl("Blade%d" % i, 0.018, h, (math.cos(a) * d, math.sin(a) * d, h / 2), (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), 0), 3, r2=0.001, material=F_LeafLight(), parent=r)

@model
def GlowPlant():
    r = empty("GlowPlant")
    st = cyl("Stem", 0.035, 1.2, (0, 0, 0.6), verts=8, r2=0.02, material=F_Leaf(), parent=r); shape(st, sub=1)
    b = uvs("Bulb", 0.2, (0, 0, 1.3), 18, 12, None, F_Glow(), r); shape(b)
    for i in range(5):
        a = i * 1.26
        lf = cyl("Leaf%d" % i, 0.09, 0.55, (math.cos(a) * 0.22, math.sin(a) * 0.22, 0.4), (math.sin(a) * 1.1, -math.cos(a) * 1.1, 0), 4, r2=0.01, material=F_Leaf(), parent=r); shape(lf, sub=1)

@model
def Mushroom_Glow():
    r = empty("Mushroom_Glow")
    s = cyl("Stalk", 0.08, 0.4, (0, 0, 0.2), verts=10, r2=0.06, material=D_Ruin(), parent=r); shape(s, sub=1)
    c = uvs("Cap", 0.3, (0, 0, 0.42), 20, 10, (1, 1, 0.5), F_Glow(), r); shape(c, disp=0.01, scale=0.1)

@model
def Rock_Mossy():
    r = empty("Rock_Mossy")
    boulder("Rock0", 0.85, (0, 0, 0.45), 3, F_Rock(), r)
    boulder("Rock1", 0.5, (0.9, 0.35, 0.25), 4, F_Rock(), r)
    m = ico("Moss", 0.6, (0, 0, 0.82), 3, (1.0, 0.9, 0.3), F_Leaf(), r); shape(m, disp=0.06, scale=0.25)

@model
def Log_Fallen():
    r = empty("Log_Fallen")
    lg = cyl("Log", 0.3, 2.7, (0, 0, 0.3), (0, math.pi / 2, 0), 14, material=F_Bark(), parent=r); shape(lg, sub=1, disp=0.04, scale=0.3)
    m = ico("Moss", 0.32, (0.3, 0, 0.55), 3, (1.6, 0.8, 0.4), F_Leaf(), r); shape(m, disp=0.05, scale=0.2)

@model
def WaterGate():
    r = empty("WaterGate")
    for s in (-1, 1):
        p = cube("Post%s" % ("L" if s < 0 else "R"), (0.5, 0.6, 2.4), (s * 1.4, 0, 1.2), material=F_Rock(), parent=r); _stone(p, 0.02)
    l = cube("Lintel", (3.4, 0.6, 0.4), (0, 0, 2.6), material=F_Rock(), parent=r); _stone(l, 0.02)
    g = cube("Gate", (2.3, 0.2, 2.0), (0, 0, 1.0), material=F_Root(), parent=r); shape(g, bevel=0.02, sub=1, disp=0.02, scale=0.2)
    w = empty("Wheel", (0, -0.45, 1.5)); w.parent = r
    wr = torus("WheelRing", 0.42, 0.06, (0, 0, 0), (math.pi / 2, 0, 0), 24, 8, F_Root(), w); shape(wr, disp=0.01, scale=0.1)
    for i in range(4):
        sp = cube("Spoke%d" % i, (0.06, 0.06, 0.8), (0, 0, 0), (0, math.pi * i / 4, 0), F_Root(), w); sp.parent = w; shape(sp, bevel=0.01, sub=1)

@model
def RootSocket():
    r = empty("RootSocket")
    b = cyl("Base", 0.9, 0.3, (0, 0, 0.15), verts=16, material=F_Rock(), parent=r); _stone(b)
    c = cyl("Cup", 0.6, 0.35, (0, 0, 0.45), verts=16, r2=0.45, material=F_Rock(), parent=r); _stone(c)
    torus("Rim", 0.5, 0.06, (0, 0, 0.64), (0, 0, 0), 20, 6, F_Root(), r)
    for i in range(4):
        a = i * 1.6
        s = cyl("Sprout%d" % i, 0.08, 0.55, (math.cos(a) * 0.68, math.sin(a) * 0.68, 0.3), (math.sin(a) * 0.8, -math.cos(a) * 0.8, 0), 6, r2=0.01, material=F_Root(), parent=r); shape(s, sub=1, disp=0.01, scale=0.1)

@model
def MemorySeed():
    r = empty("MemorySeed")
    s = ico("Seed", 0.16, (0, 0, 0.2), 3, (0.8, 0.8, 1.3), S_Gold(), r); shape(s)
    torus("Halo", 0.26, 0.012, (0, 0, 0.2), (math.pi / 2.5, 0, 0), 24, 5, S_Gold(), r)

@model
def RootBridge():
    """Segments Root_00 ... Root_11 so Unity can grow the bridge by enabling them in order. 7 m long along +Y."""
    r = empty("RootBridge")
    n = 12
    for i in range(n):
        y = 0.6 * i - 0.1
        z = -0.1 + 0.18 * math.sin(math.pi * i / (n - 1))
        for k, x in enumerate((-0.7, 0.0, 0.7)):
            c = cyl("Root_%02d_%d" % (i, k), 0.30, 0.9, (x, y, z), (math.pi / 2, 0, 0), 10, r2=0.24, material=F_Root(), parent=r)
            shape(c, sub=1, disp=0.03, scale=0.25)
        p = cube("Plank_%02d" % i, (2.0, 0.62, 0.12), (0, y, z + 0.28), material=F_Root(), parent=r); shape(p, bevel=0.02, sub=1, disp=0.012, scale=0.15)
        g = ico("Bloom_%02d" % i, 0.09, (0.9 if i % 2 else -0.9, y, z + 0.35), 2, None, F_Glow(), r); shape(g)
    for y, nm in ((-0.7, "StumpA"), (7.0, "StumpB")):
        st = cyl(nm, 0.9, 0.8, (0, y, 0.2), verts=14, r2=0.7, material=F_Bark(), parent=r); shape(st, sub=1, disp=0.06, scale=0.4)

# ================================================================== MIRROR + ROOM
@model
def Mirror_Intact():
    r = empty("Mirror_Intact")
    f = cube("FrameOuter", (1.6, 0.14, 2.4), (0, 0, 1.4), material=S_Frame(), parent=r); shape(f, bevel=0.03, sub=1)
    cube("Glass", (1.3, 0.04, 2.1), (0, -0.09, 1.4), material=S_Mirror(), parent=r)
    s = cube("Stand", (1.0, 0.5, 0.2), (0, 0.1, 0.1), material=S_Frame(), parent=r); shape(s, bevel=0.03, sub=1)

@model
def Mirror_Shards():
    """20 shard pieces (Shard00..Shard19) tiling the glass area; Unity scatters them when it shatters."""
    r = empty("Mirror_Shards")
    rnd = random.Random(23)
    cols, rows = 4, 5
    w, h = 1.3 / cols, 2.1 / rows
    k = 0
    for j in range(rows):
        for i in range(cols):
            x = -0.65 + w * (i + 0.5); z = 0.35 + h * (j + 0.5)
            a = rnd.uniform(0.7, 1.1)
            pts = [(-w * a / 2, -h * a / 2), (w * a / 2, -h * a / 2 * rnd.uniform(0.6, 1.0)), (w * a / 2 * rnd.uniform(0.5, 1), h * a / 2), (-w * a / 2, h * a / 2 * rnd.uniform(0.7, 1))]
            poly("Shard%02d" % k, pts, 0.025, (x, -0.1, z), (math.pi / 2, 0, 0), S_Mirror(), r)
            k += 1

@model
def MirrorFragment():
    r = empty("MirrorFragment")
    pts = [(-0.2, -0.3), (0.28, -0.22), (0.18, 0.12), (0.02, 0.34), (-0.26, 0.1)]
    poly("Shard", pts, 0.035, (0, 0, 0.5), (math.pi / 2, 0, 0), S_Mirror(), r)
    torus("GlowRing", 0.42, 0.012, (0, 0, 0.55), (0, 0, 0), 28, 4, S_Gold(), r)

@model
def MirrorRoom():
    r = empty("MirrorRoom")
    cube("Floor", (6, 6, 0.2), (0, 0, -0.1), material=S_Floor(), parent=r)
    cube("WallBack", (6, 0.2, 3.2), (0, 3, 1.6), material=S_Wall(), parent=r)
    cube("WallLeft", (0.2, 6, 3.2), (-3, 0, 1.6), material=S_Wall(), parent=r)
    cube("WallRight", (0.2, 6, 3.2), (3, 0, 1.6), material=S_Wall(), parent=r)
    cube("Ceiling", (6, 6, 0.2), (0, 0, 3.3), material=S_Wall(), parent=r)
    for nm, loc, size in (("BaseBack", (0, 2.88, 0.1), (6, 0.05, 0.2)), ("BaseLeft", (-2.88, 0, 0.1), (0.05, 6, 0.2)), ("BaseRight", (2.88, 0, 0.1), (0.05, 6, 0.2))):
        cube(nm, size, loc, material=D_Wood(), parent=r)
    rg = cube("Rug", (2.4, 1.6, 0.03), (0, -1.0, 0.02), material=D_Ruin(), parent=r); shape(rg, bevel=0.01, sub=1)
    bd = cube("Bed", (1.8, 0.9, 0.35), (-2.0, 1.8, 0.18), material=D_Wood(), parent=r); shape(bd, bevel=0.04, sub=1)
    pl = cube("Pillow", (0.5, 0.3, 0.12), (-2.55, 1.8, 0.42), material=S_Wall(), parent=r); shape(pl, bevel=0.04, sub=1)
    cube("Window", (1.0, 0.1, 1.2), (2.9, 0.5, 1.6), (0, 0, math.pi / 2), S_Mirror(), r)

# ================================================================== PREVIEW overrides (bigger terrains)

def apply_preview_textures():
    """Preview only: attach the generated PNG textures so the renders show surface detail (not exported)."""
    tdir = os.path.join(ROOT, "Assets", "TwoWorlds", "Textures")
    for m in bpy.data.materials:
        base = m.name[2:] if m.name.startswith("M_") else m.name
        ap = os.path.join(tdir, base + "_albedo.png"); npth = os.path.join(tdir, base + "_normal.png")
        if not os.path.exists(ap) or m.node_tree is None: continue
        nt = m.node_tree; bsdf = nt.nodes.get("Principled BSDF")
        t1 = nt.nodes.new("ShaderNodeTexImage"); t1.image = bpy.data.images.load(ap, check_existing=True)
        nt.links.new(t1.outputs["Color"], bsdf.inputs["Base Color"])

# ------------------------------------------------------------------ export + preview
def export(name):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.data.objects:
        o.select_set(True)
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False,
        add_leaf_bones=False, use_mesh_modifiers=True)
    return path

def preview(name):
    apply_preview_textures()
    os.makedirs(PREV, exist_ok=True)
    pts = []
    for o in bpy.data.objects:
        if o.type == 'MESH':
            pts += [o.matrix_world @ v.co for v in o.data.vertices]
    if not pts: return
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (mn + mx) / 2; size = (mx - mn).length
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    bpy.context.collection.objects.link(cam)
    cam.location = c + Vector((0.7, -1.0, 0.55)).normalized() * size * 1.5
    d = c - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.camera = cam
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.render.resolution_x = sc.render.resolution_y = 384
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.render.film_transparent = False
    if sc.world is None: sc.world = bpy.data.worlds.new("W")
    sc.world.color = (0.18, 0.2, 0.24)
    sc.render.filepath = os.path.join(PREV, name + ".png")
    bpy.ops.render.render(write_still=True)

def main():
    names = [n for n in MODELS if (ONLY is None or n in ONLY) and not n.startswith("Human_")]  # characters now come from Art/Source/Characters_v2.blend
    for n in names:
        reset()
        MODELS[n]()
        p = export(n)
        if PREVIEW: preview(n)
        print("EXPORTED", n)
    print("DONE", len(names), "models ->", OUT)

main()
