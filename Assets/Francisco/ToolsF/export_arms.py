# Exports Arms1.blend to an FBX that Unity's TrackedArms script can drive.
# It never modifies the .blend. Re-run after editing the model:
#   /Applications/Blender.app/Contents/MacOS/Blender -b "<path>/Arms1.blend" --python Assets/Francisco/ToolsF/export_arms.py -- Assets/Francisco/ModelsF/Resources/Arms.fbx
#
# What it does: keeps only the arm mesh + armature, repairs the mesh for a game engine (faces
# pointing inwards, finger weights bleeding into neighbouring fingers, max 4 bones per vertex),
# renames the bones to the hand-joint names the Unity script looks up, scales the arm to
# real-world size, and adds a mirrored copy so there is a left and a right arm.
import bpy, sys
from mathutils import Matrix, Vector

out_path = sys.argv[sys.argv.index("--") + 1]
HAND_LENGTH_M = 0.185  # wrist to middle fingertip, in metres

# Blender bone name -> name used in Unity (matches the HandJoint enum in HandData.cs)
BONES = {
    "Bone": "UpperArm", "Bone.001": "Forearm",
    "Bone.006": "ThumbMetacarpal", "Bone.007": "ThumbProximal", "Bone.008": "ThumbDistal",
    "Bone.005": "IndexMetacarpal", "Bone.009": "IndexProximal", "Bone.010": "IndexIntermediate", "Bone.011": "IndexDistal",
    "Bone.004": "MiddleMetacarpal", "Bone.012": "MiddleProximal", "Bone.013": "MiddleIntermediate", "Bone.014": "MiddleDistal",
    "Bone.003": "RingMetacarpal", "Bone.015": "RingProximal", "Bone.016": "RingIntermediate", "Bone.017": "RingDistal",
    "Bone.002": "LittleMetacarpal", "Bone.018": "LittleProximal", "Bone.019": "LittleIntermediate", "Bone.020": "LittleDistal",
}

arm = bpy.data.objects["Armature"]
mesh = next(o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm)
missing = [b for b in BONES if b not in arm.data.bones]
if missing:
    raise SystemExit("Armature is missing expected bones: %s" % missing)

for o in list(bpy.data.objects):
    if o not in (arm, mesh):
        bpy.data.objects.remove(o, do_unlink=True)

# ---- Mesh repair (Blender hides these problems, a game engine does not) ----
import bmesh
from mathutils.geometry import intersect_point_line

# 1. Faces pointing inwards are invisible in Unity. Make every face point outwards.
bm = bmesh.new()
bm.from_mesh(mesh.data)
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
before = {f.index: f.normal.copy() for f in bm.faces}
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
flipped = sum(1 for f in bm.faces if f.normal.dot(before[f.index]) < 0)
if bm.calc_volume(signed=True) < 0:
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
bm.to_mesh(mesh.data)
bm.free()
print("EXPORT repaired %d inward-facing faces, removed %d loose vertices" % (flipped, len(loose)))

# 2. Skin weights. Some vertices of one finger are weighted to a neighbouring finger's bones, so
#    bending one finger drags its neighbours. Each finger vertex keeps only its own finger's bones.
#    Game engines also use at most 4 bones per vertex, so keep the 4 strongest and normalise.
FINGERS = [["Bone.006", "Bone.007", "Bone.008"],
           ["Bone.005", "Bone.009", "Bone.010", "Bone.011"],
           ["Bone.004", "Bone.012", "Bone.013", "Bone.014"],
           ["Bone.003", "Bone.015", "Bone.016", "Bone.017"],
           ["Bone.002", "Bone.018", "Bone.019", "Bone.020"]]
finger_of = {b: i for i, chain in enumerate(FINGERS) for b in chain}
phalanges = {b for chain in FINGERS for b in chain[1:]}
segments = {b.name: (arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local) for b in arm.data.bones}

def bone_distance(point, bone):
    head, tail = segments[bone]
    closest, t = intersect_point_line(point, head, tail)
    t = max(0.0, min(1.0, t))
    return (point - (head + (tail - head) * t)).length

group_name = {g.index: g.name for g in mesh.vertex_groups}
fixed = 0
for v in mesh.data.vertices:
    point = mesh.matrix_world @ v.co
    weights = {group_name[g.group]: g.weight for g in v.groups if g.weight > 1e-4 and group_name[g.group] in BONES}
    nearest = min(segments, key=lambda b: bone_distance(point, b))
    cleaned = dict(weights)
    if nearest in finger_of:
        cleaned = {b: w for b, w in weights.items() if not (b in phalanges and finger_of[b] != finger_of[nearest])}
    total = sum(cleaned.values())
    if total < 0.25 * max(sum(weights.values()), 1e-6):
        cleaned = {nearest: 1.0}  # it was almost entirely on the wrong finger
    cleaned = dict(sorted(cleaned.items(), key=lambda kv: -kv[1])[:4])
    total = sum(cleaned.values())
    cleaned = {b: w / total for b, w in cleaned.items()}
    if set(cleaned) != set(weights) or any(abs(cleaned[b] - weights[b]) > 0.02 for b in cleaned):
        fixed += 1
    for g in mesh.vertex_groups:
        if g.name in cleaned:
            g.add([v.index], cleaned[g.name], 'REPLACE')
        else:
            g.remove([v.index])
print("EXPORT corrected the bone weights of %d of %d vertices" % (fixed, len(mesh.data.vertices)))

# Bake object transforms into the data, put the shoulder at the origin, scale to real size.
bones = arm.data.bones
mid_len = sum(bones[b].length for b in ("Bone.004", "Bone.012", "Bone.013", "Bone.014"))
factor = HAND_LENGTH_M / mid_len
shoulder = arm.matrix_world @ bones["Bone"].head_local
T = Matrix.Scale(factor, 4) @ Matrix.Translation(-shoulder)
mesh_world, arm_world = mesh.matrix_world.copy(), arm.matrix_world.copy()
mesh.data.transform(T @ mesh_world)
arm.data.transform(T @ arm_world)
arm.matrix_world = Matrix.Identity(4)
mesh.parent = arm
mesh.matrix_parent_inverse = Matrix.Identity(4)
mesh.matrix_basis = Matrix.Identity(4)

def make_side(prefix, src_arm, src_mesh, mirror):
    a = src_arm.copy(); a.data = src_arm.data.copy()
    m = src_mesh.copy(); m.data = src_mesh.data.copy()
    for o in (a, m):
        bpy.context.scene.collection.objects.link(o)
    m.parent = a
    m.matrix_parent_inverse = Matrix.Identity(4)
    for mod in m.modifiers:
        if mod.type == 'ARMATURE':
            mod.object = a
    if mirror:
        M = Matrix.Scale(-1, 4, Vector((0, 1, 0)))
        m.data.transform(M)
        m.data.flip_normals()
        a.data.transform(M)
    a.name = prefix + "Arm"; a.data.name = prefix + "ArmRig"
    m.name = prefix + "ArmMesh"; m.data.name = prefix + "ArmMesh"
    for old, new in BONES.items():
        a.data.bones[old].name = prefix + "_" + new
        vg = m.vertex_groups.get(old)
        if vg: vg.name = prefix + "_" + new
    return a, m

# The modelled arm is a LEFT arm (palm down, thumb on the inner side).
left = make_side("Left", arm, mesh, False)
right = make_side("Right", arm, mesh, True)
bpy.data.objects.remove(mesh, do_unlink=True)
bpy.data.objects.remove(arm, do_unlink=True)

# Report sizes (and the forearm's top surface height, used to seat the forearm buttons).
a, m = left
fb = a.data.bones["Left_Forearm"]
axis = (fb.tail_local - fb.head_local).normalized()
print("EXPORT scale factor %.4f | upper arm %.3f m | forearm %.3f m | hand %.3f m" % (factor, a.data.bones["Left_UpperArm"].length, fb.length, HAND_LENGTH_M))
for back in (0.04, 0.08, 0.12):
    p = fb.tail_local - axis * back
    near = [v.co for v in m.data.vertices if abs((v.co - p).dot(axis)) < 0.02]
    if near:
        print("EXPORT forearm %.2f m behind wrist: top surface %.4f m above bone, half-width %.4f m" % (back, max(c.z - p.z for c in near), max(abs(c.y - p.y) for c in near)))

bpy.ops.object.select_all(action='DESELECT')
for o in left + right:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=out_path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         add_leaf_bones=True, bake_anim=False, apply_scale_options='FBX_SCALE_UNITS',
                         bake_space_transform=True, mesh_smooth_type='FACE', use_armature_deform_only=False)
print("EXPORT wrote", out_path)
