"""Editable Blender source for three coordinated Mimo sets. No raster art is fabricated here.
The fabric albedos are original image_gen outputs kept in Resources/Cosmetics.
Coordinates: X left/right, Blender Y depth (front negative), Z height, body 0..1.
"""
import bpy, math, os, sys
from mathutils import Vector
OUT=os.path.join(os.path.dirname(os.path.dirname(__file__)), 'Assets','Resources','Cosmetics')
os.makedirs(OUT,exist_ok=True)

def mat(name,color):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);return m
def finish(o,m):
    o.data.materials.append(m)
    for p in o.data.polygons:p.use_smooth=True
    return o
def orb(name,p,s,m):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=p)
    o=bpy.context.object;o.name=name;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,m)
def tube(name,points,r,m,closed=False):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=8;c.bevel_depth=r;c.bevel_resolution=3
    q=c.splines.new('POLY');q.points.add(len(points)-1)
    for a,p in zip(q.points,points):a.co=(*p,1)
    q.use_cyclic_u=closed;o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(m);return o
def torus(name,z,rx,ry,r,m):
    return tube(name,[(rx*math.cos(i*math.tau/72),ry*math.sin(i*math.tau/72),z) for i in range(72)],r,m,True)
def surface(name,vs,fs,m):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);finish(o,m)
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.1,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
    return o
def leaf(name,origin,end,width,m):
    a=Vector(origin);b=Vector(end);d=b-a;normal=Vector((-d.z,0,d.x)).normalized()
    vs=[];fs=[];rows=20;cols=8
    for i in range(rows+1):
        t=i/rows;w=width*math.sin(math.pi*t)**.7
        for j in range(cols+1):
            v=(j/cols*2-1);p=a+d*t+normal*w*v;p.y+=.035*math.sin(math.pi*t)*(1-v*v);vs.append(tuple(p))
    for i in range(rows):
        for j in range(cols):k=i*(cols+1)+j;fs.append((k,k+1,k+cols+2,k+cols+1))
    o=surface(name,vs,fs,m);solid=o.modifiers.new('Soft leaf thickness','SOLIDIFY');solid.thickness=.008;return o
def flower(p,size,pearl,gold):
    for n in range(9):
        a=n*math.tau/9;o=orb('Sculpted daisy petal',(p[0]+math.cos(a)*size*.6,p[1]-.008,p[2]+math.sin(a)*size*.6),(size*.43,.025,size*.17),pearl);o.rotation_euler.y=-a
    orb('Daisy gold pollen heart',(p[0],p[1]-.035,p[2]),(size*.25,.032,size*.25),gold)
def crescent(p,r,m):
    # Rounded open lunar silhouette: two arcs sharing their tips, extruded then bevelled.
    points=[]
    for i in range(33):
        a=math.radians(55+250*i/32);points.append((math.cos(a)*r,math.sin(a)*r))
    for i in range(33):
        a=math.radians(305-250*i/32);points.append((math.cos(a)*r*.72+r*.25,math.sin(a)*r*.90))
    vs=[(p[0]+x,p[1]+dep,p[2]+z) for dep in (-.015,.015) for x,z in points];N=len(points)
    fs=[tuple(range(N-1,-1,-1)),tuple(range(N,2*N))]+[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
    o=surface('Bevelled champagne crescent',vs,fs,m);b=o.modifiers.new('Jeweller rounded edges','BEVEL');b.width=.008;b.segments=3
def star(p,r,m):
    points=[(math.cos(i*math.pi/5+math.pi/2)*r*(1 if i%2==0 else .46),math.sin(i*math.pi/5+math.pi/2)*r*(1 if i%2==0 else .46)) for i in range(10)]
    vs=[(p[0]+x,p[1]+dep,p[2]+z) for dep in (-.012,.012) for x,z in points]
    fs=[tuple(range(9,-1,-1)),tuple(range(10,20))]+[(i,(i+1)%10,(i+1)%10+10,i+10) for i in range(10)]
    o=surface('Gold star embroidery',vs,fs,m);b=o.modifiers.new('Rounded star edge','BEVEL');b.width=.004;b.segments=2

for kit in ['aurora','luna','mar']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    fabric=mat('fabric_'+kit,{'aurora':(.55,.8,.62),'luna':(.57,.46,.84),'mar':(.4,.77,.84)}[kit]);gold=mat('gold',(.94,.73,.33));pearl=mat('pearl',(1,.95,.87))
    if kit=='aurora':
        torus('Daisy crown vine',1.01,.37,.25,.013,gold)
        for i in range(9):
            a=math.pi+math.pi*i/8;flower((.36*math.cos(a),.26*math.sin(a),1.055+.018*math.cos(a*2)),.073,pearl,gold)
        for side in [-1,1]:
            for i in range(3):
                origin=(side*.32,.19,.62);end=(side*(.73+.05*i),.24,.95-.23*i)
                leaf('Silk leaf wing',origin,end,.12,fabric)
                tube('Gold wing midrib',[origin,((origin[0]+end[0])/2,.24,(origin[2]+end[2])/2),end],.006,gold)
            flower((side*.34,.15,.66),.055,pearl,gold)
    if kit=='luna':
        torus('Lunar hat rolled brim',.84,.36,.27,.035,fabric)
        vs=[];fs=[];N=48;R=28
        for j in range(R+1):
            t=j/R;radius=.32*(1-t)**.85+.012
            for i in range(N):
                a=i*math.tau/N;vs.append((radius*math.cos(a)+.20*t*t,.78*radius*math.sin(a)+.02,.84+.47*t-.10*t**5))
        for j in range(R):
            for i in range(N):a=j*N+i;b=j*N+(i+1)%N;fs.append((a,b,b+N,a+N))
        surface('Curved velvet lunar hat',vs,fs,fabric);torus('Champagne hat band',.905,.29,.23,.009,gold);crescent((.02,-.23,1.04),.08,gold)
        vs=[];fs=[];N=28;R=15
        for j in range(R+1):
            t=j/R
            for i in range(N+1):
                a=(i/N-.5)*math.pi*1.1;r=.48+.14*t;vs.append((math.sin(a)*r,math.cos(a)*r,.82-.58*t+.045*math.cos(i/N*math.pi*6)*t))
        for j in range(R):
            for i in range(N):k=j*(N+1)+i;fs.append((k,k+1,k+N+2,k+N+1))
        o=surface('Draped constellation cape',vs,fs,fabric);s=o.modifiers.new('Cape tailored thickness','SOLIDIFY');s.thickness=.013
        for side in [-1,1]:
            star((side*.42,-.33,.76),.055,gold)
            tube('Soft cape collar',[(side*.40,-.32,.78),(side*.22,-.51,.73),(0,-.55,.70)],.014,gold)
        crescent((0,-.565,.7),.045,gold)
    if kit=='mar':
        # Rounded nacre beret with a sculpted upright scallop applique.
        orb('Soft nacre beret',(0,0,.885),(.38,.29,.10),fabric)
        vs=[];fs=[];N=48;R=16
        for j in range(R+1):
            t=j/R
            for i in range(N+1):
                a=math.pi*(i/N);wave=1+.045*math.cos(a*12);vs.append((math.cos(a)*.25*t*wave,-.27-.055*math.sin(math.pi*t/2),.855+math.sin(a)*.20*t))
        for j in range(R):
            for i in range(N):k=j*(N+1)+i;fs.append((k,k+N+1,k+N+2,k+1))
        o=surface('Nacre sculpted scallop beret',vs,fs,pearl);s=o.modifiers.new('Scallop shell thickness','SOLIDIFY');s.thickness=.025
        for i in range(13):
            a=math.pi*i/12;tube('Shell gold radial ridge',[(math.cos(a)*.25*t/12,-.28-.055*math.sin(math.pi*t/24),.855+math.sin(a)*.20*t/12) for t in range(13)],.004,gold)
        for i in range(17):
            a=math.pi+math.pi*i/16;orb('Hand-set necklace pearl',(.54*math.cos(a),.60*math.sin(a)-.035,.25+.07*abs(math.cos(a))),(.026,.026,.026),pearl)
        orb('Ocean opal pendant',(0,-.655,.20),(.044,.028,.055),fabric)
        for side in [-1,1]:
            for i in range(3):
                start=(side*.32,.15,.51);end=(side*(.62+.025*i),.18,.71-.12*i);leaf('Pearl-edged sea fin',start,end,.067,fabric)
                tube('Sea fin pearl edge',[start,end],.005,pearl)
    # Apply modifiers, UV unwrap curves, join by material for a small stable draw budget.
    for o in list(bpy.context.scene.objects):
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        if o.type=='CURVE':bpy.ops.object.convert(target='MESH')
        for mod in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.join();obj=bpy.context.object;obj.name='MimoKit_'+kit
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.1,island_margin=.012);bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),kit+'.blend'))
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,kit+'.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False)
    print('MIMO_KIT_EXPORTED',kit,len(obj.data.vertices),len(obj.data.polygons))
