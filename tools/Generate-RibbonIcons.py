from pathlib import Path
import cairosvg
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1] / 'src' / 'YangTools.Revit' / 'Icons'
ROOT.mkdir(parents=True, exist_ok=True)

STROKE = '#355D7A'
ACCENT = '#6E9FC4'
SW = 1.85


def svg_start():
    return ET.Element('svg', {
        'xmlns':'http://www.w3.org/2000/svg','viewBox':'0 0 32 32','width':'32','height':'32',
        'fill':'none','stroke':STROKE,'stroke-width':str(SW),'stroke-linecap':'round','stroke-linejoin':'round'
    })

def add(parent, tag, **attrs):
    e=ET.SubElement(parent, tag, {k.replace('_','-'):str(v) for k,v in attrs.items()})
    return e

def line(g,x1,y1,x2,y2,accent=False,dash=None):
    a={'x1':x1,'y1':y1,'x2':x2,'y2':y2}
    if accent:a['stroke']=ACCENT
    if dash:a['stroke_dasharray']=dash
    add(g,'line',**a)

def rect(g,x,y,w,h,r=1.5,accent=False):
    a={'x':x,'y':y,'width':w,'height':h,'rx':r}
    if accent:a['stroke']=ACCENT
    add(g,'rect',**a)

def circle(g,cx,cy,r,accent=False):
    a={'cx':cx,'cy':cy,'r':r}
    if accent:a['stroke']=ACCENT
    add(g,'circle',**a)

def path(g,d,accent=False,fill='none'):
    a={'d':d,'fill':fill}
    if accent:a['stroke']=ACCENT
    add(g,'path',**a)

def poly(g,pts,accent=False,closed=False):
    tag='polygon' if closed else 'polyline'
    a={'points':' '.join(f'{x},{y}' for x,y in pts)}
    if accent:a['stroke']=ACCENT
    add(g,tag,**a)

def dot(g,cx,cy,r=1.4,accent=True):
    add(g,'circle',cx=cx,cy=cy,r=r,fill=ACCENT if accent else STROKE,stroke='none')


def icon(name, draw):
    root=svg_start(); draw(root)
    data=ET.tostring(root,encoding='unicode')
    for size in (32,16):
        cairosvg.svg2png(bytestring=data.encode(),write_to=str(ROOT/f'{name}_{size}.png'),output_width=size,output_height=size)


def align_line(g):
    line(g,5,24,27,24,accent=True)
    line(g,9,8,9,21); line(g,6,8,12,8); line(g,6,14,12,14)
    line(g,18,10,18,21); line(g,15,10,22,10); line(g,15,16,22,16)
    line(g,9,21,9,24,accent=True); line(g,18,21,18,24,accent=True)

def align_text(g):
    line(g,6,7,15,7); line(g,10.5,7,10.5,23); line(g,6.5,14,14.5,14)
    line(g,18,10,27,10,accent=True); line(g,22.5,10,22.5,23,accent=True); line(g,18.5,17,26.5,17,accent=True)
    line(g,4,25,28,25)

def batch_task(g):
    rect(g,6,5,15,18,2); rect(g,10,9,16,18,2,accent=True)
    line(g,10,10,17,10); line(g,10,14,17,14); line(g,10,18,15,18)
    path(g,'M18 21c2.5-3 6.5-1.2 6.5 1.5 2.8.2 3.2 4.5.2 4.5H18.5c-3 0-3.2-4.2-.5-4.6')

def boolean_geometry(g):
    circle(g,12.5,16,7); circle(g,19.5,16,7,accent=True)
    path(g,'M16 9.9c2.1 1.2 3.5 3.5 3.5 6.1S18.1 20.9 16 22.1c-2.1-1.2-3.5-3.5-3.5-6.1S13.9 11.1 16 9.9')

def cn_check(g):
    rect(g,5,4,18,24,2)
    line(g,9,10,19,10); line(g,9,15,19,15); line(g,9,20,15,20)
    circle(g,23,22,5,accent=True); poly(g,[(20.5,22),(22.3,24),(25.8,19.8)],accent=True)

def column_path(g):
    path(g,'M4 23C10 7 19 7 28 18',accent=True)
    for x,y,a in [(8,16,0),(14,11,0),(20,12.2,0),(25,15.5,0)]:
        rect(g,x-1.8,y-3.2,3.6,6.4,.6)
    line(g,4,26,28,26)

def copilot(g):
    path(g,'M6 7h20v14H14l-5 4v-4H6z')
    circle(g,13,14,1.2,accent=True); circle(g,19,14,1.2,accent=True)
    path(g,'M22 5l.8 2.1L25 8l-2.2.9L22 11l-.8-2.1L19 8l2.2-.9z',accent=True)

def dim_override(g):
    line(g,6,10,26,10); poly(g,[(6,10),(9,8),(9,12)],closed=False); poly(g,[(26,10),(23,8),(23,12)],closed=False)
    line(g,6,6,6,14); line(g,26,6,26,14)
    path(g,'M11 23l8-8 3 3-8 8-4 1z',accent=True); line(g,18,16,21,19,accent=True)

def distribute_text(g):
    for y in (7,16,25):
        line(g,7,y,19,y); line(g,10,y-3,10,y+3)
    line(g,24,8,24,24,accent=True); poly(g,[(22,10),(24,8),(26,10)],accent=True); poly(g,[(22,22),(24,24),(26,22)],accent=True)

def loft(g):
    path(g,'M8 6c4-2 12-2 16 0-4 3-12 3-16 0z')
    path(g,'M6 16c5-3 15-3 20 0-5 3-15 3-20 0z',accent=True)
    path(g,'M9 26c4-2 10-2 14 0-4 2-10 2-14 0z')
    line(g,8,6,6,16); line(g,24,6,26,16); line(g,6,16,9,26); line(g,26,16,23,26)

def solid_extract(g):
    # Source solid on the left, extracted family/object on the right.
    poly(g,[(5,12),(11,8),(17,12),(11,16)],closed=True)
    poly(g,[(5,12),(5,20),(11,24),(11,16)],closed=True)
    poly(g,[(11,16),(17,12),(17,20),(11,24)],closed=True)
    line(g,18.5,16,24,16,accent=True)
    poly(g,[(22,13.5),(25,16),(22,18.5)],accent=True)
    rect(g,22,7,6,6,1,accent=True)
    rect(g,22,20,6,6,1,accent=True)

def face_based(g):
    poly(g,[(5,20),(16,13),(27,20),(16,27)],accent=True,closed=True)
    rect(g,11,5,10,10,1)
    line(g,11,5,16,2); line(g,21,5,16,2); line(g,16,2,16,13)

def family_instance(g):
    for x,y in [(6,6),(17,6),(6,17),(17,17)]: rect(g,x,y,9,9,1.5)
    dot(g,10.5,10.5,1); dot(g,21.5,21.5,1)

def family_manager(g):
    path(g,'M4 9h9l2 3h13v14H4z')
    poly(g,[(11,17),(16,14),(21,17),(16,20)],accent=True,closed=True)
    line(g,11,17,11,22); line(g,21,17,21,22); line(g,11,22,16,25); line(g,21,22,16,25); line(g,16,20,16,25)

def level_modifier(g):
    line(g,5,8,20,8); line(g,5,16,20,16); line(g,5,24,20,24)
    line(g,25,7,25,25,accent=True); poly(g,[(22,10),(25,7),(28,10)],accent=True); poly(g,[(22,22),(25,25),(28,22)],accent=True)

def linear_placement(g):
    path(g,'M4 23C10 8 20 8 28 18',accent=True)
    for x,y in [(8,16),(13,12),(18,11),(23,14),(27,18)]: circle(g,x,y,2)

def merge_text(g):
    line(g,5,7,15,7); line(g,5,12,15,12); line(g,5,20,15,20); line(g,5,25,15,25)
    path(g,'M16 9c4 0 4 7 7 7',accent=True); path(g,'M16 22c4 0 4-6 7-6',accent=True)
    line(g,22,16,28,16,accent=True); poly(g,[(26,14),(28,16),(26,18)],accent=True)

def micro_tool(g):
    rect(g,5,9,22,17,2); path(g,'M11 9V6h10v3')
    line(g,5,15,27,15,accent=True); rect(g,13,13,6,5,1,accent=True)

def override_cleaner(g):
    path(g,'M5 22l10-14 8 6-10 14H7z',accent=True)
    line(g,12,12,20,18); line(g,4,27,27,27)
    path(g,'M21 7l2-2m-1 5l4-1m-7-3l-1-3')

def paste_cad(g):
    rect(g,7,6,16,21,2); rect(g,11,4,8,5,1)
    poly(g,[(10,22),(14,15),(18,20),(22,12)],accent=True)
    circle(g,10,22,1,accent=True); circle(g,14,15,1,accent=True); circle(g,18,20,1,accent=True); circle(g,22,12,1,accent=True)

def parameter_cleanup(g):
    # Two duplicate parameter records converge into one retained record; check mark denotes safe cleanup.
    rect(g,5,5,16,9,1.5)
    rect(g,8,10,16,9,1.5,accent=True)
    line(g,8,8.5,16,8.5)
    line(g,11,13.5,19,13.5,accent=True)
    path(g,'M11 24c3-4 7-4 10-1',accent=True)
    poly(g,[(18.5,20.5),(21,23),(25.5,17.5)],accent=True)
    line(g,7,26,16,26)

def project_info(g):
    path(g,'M7 4h12l6 6v18H7z'); poly(g,[(19,4),(19,10),(25,10)],accent=True)
    circle(g,16,19,4,accent=True); line(g,16,18,16,23,accent=True); dot(g,16,15.8,.8,True)

def project_inspector(g):
    rect(g,5,6,10,10,1); rect(g,17,6,10,10,1); rect(g,5,18,10,10,1)
    circle(g,21,22,5,accent=True); line(g,24.5,25.5,28,29,accent=True)



def cad_to_revit(g):
    # CAD polyline on the left rebuilt into a native Revit-like framed element on the right.
    poly(g,[(4,22),(8,11),(13,16),(17,8)],accent=True)
    for x,y in [(4,22),(8,11),(13,16),(17,8)]: dot(g,x,y,1)
    line(g,17,15,21,15,accent=True)
    poly(g,[(19,12.5),(22,15),(19,17.5)],accent=True)
    rect(g,22,7,6,16,1)
    line(g,24,10,26,10); line(g,24,14,26,14); line(g,24,18,26,18)

def rebar_test(g):
    # Generic rebar test/lab entry: bar + inspection magnifier.
    path(g,'M5 20c2-7 5-11 11-14 5 3 8 7 10 14',accent=True)
    line(g,7,22,22,22)
    circle(g,20,12,5)
    line(g,23.5,15.5,27,19,accent=True)
    dot(g,11,13,1.1)

def rebar_gradient_set(g):
    # Two control bars with a graded family between them and a vertical distribution axis.
    path(g,'M6 24h9c2 0 3-1 3-3v-3',accent=False)
    path(g,'M6 8h13c2 0 3 1 3 3v3',accent=False)
    line(g,9,20,18,20,accent=True)
    line(g,8,16,20,16,accent=True)
    line(g,7,12,21,12,accent=True)
    line(g,26,7,26,25,accent=True)
    poly(g,[(23.5,10),(26,7),(28.5,10)],accent=True)
    poly(g,[(23.5,22),(26,25),(28.5,22)],accent=True)

def rebar_face(g):
    poly(g,[(5,21),(16,14),(27,21),(16,27)],closed=True)
    path(g,'M8 19h9c3 0 5-2 5-5',accent=True)
    line(g,8,16,8,22,accent=True)

def rebar_surface_varying(g):
    poly(g,[(5,24),(16,16),(27,24),(16,29)],closed=True)
    for x,h in [(9,8),(13,10),(17,12),(21,14)]:
        line(g,x,22,x,22-h,accent=True)
    line(g,7,22,24,22)

def rebar_shape_create(g):
    poly(g,[(6,22),(6,10),(16,10),(16,18),(25,18)],accent=True)
    for x,y in [(6,22),(6,10),(16,10),(16,18),(25,18)]: dot(g,x,y,1)
    line(g,24,7,24,13); line(g,21,10,27,10)

def rebar_freeform_create(g):
    path(g,'M5 22C8 8 16 7 20 14s3 9 7 6',accent=True)
    line(g,24,6,24,12); line(g,21,9,27,9)
    dot(g,6,20,1); dot(g,20,14,1)

def rebar_shape_modify(g):
    poly(g,[(5,22),(5,11),(15,11),(15,19),(23,19)])
    path(g,'M15 26l8-8 3 3-8 8-4 1z',accent=True)
    line(g,22,19,25,22,accent=True)

def rebar_constraint(g):
    line(g,7,9,7,24); line(g,24,9,24,24)
    path(g,'M9 13h5c2 0 2 4 4 4h4',accent=True)
    poly(g,[(12,11),(14,13),(12,15)],accent=True)
    poly(g,[(20,15),(22,17),(20,19)],accent=True)

def rebar_freeform_modify(g):
    path(g,'M5 22C8 8 16 7 20 14s3 9 7 6')
    path(g,'M14 27l8-8 3 3-8 8-4 1z',accent=True)

def rebar_hook(g):
    path(g,'M6 9h12v10c0 4 5 5 7 1c1-2 0-5-3-5',accent=True)
    line(g,6,6,6,13)

def rebar_relationship(g):
    line(g,6,10,24,10); line(g,8,22,26,22)
    path(g,'M11 10c0 5 10 7 10 12',accent=True)
    poly(g,[(18.5,19.5),(21,22),(23.5,19.5)],accent=True)

def rebar_coupler(g):
    line(g,5,16,12,16); line(g,20,16,27,16)
    rect(g,12,11,8,10,1,accent=True)
    line(g,14,13,14,19,accent=True); line(g,18,13,18,19,accent=True)

def rebar_inspector(g):
    path(g,'M5 22C8 9 15 7 21 12')
    circle(g,20,19,5,accent=True)
    line(g,23.5,22.5,27,26,accent=True)
    dot(g,10,15,1)

def rebar_clearance(g):
    line(g,7,8,7,24); line(g,24,8,24,24)
    line(g,10,16,21,16,accent=True)
    poly(g,[(12,13.5),(10,16),(12,18.5)],accent=True)
    poly(g,[(19,13.5),(21,16),(19,18.5)],accent=True)
    line(g,15.5,10,15.5,13,accent=True); line(g,15.5,19,15.5,22,accent=True)

def settings(g):
    circle(g,16,16,4,accent=True)
    path(g,'M16 4l2 1 2.5-.5 1.5 2-1 2.3 1.5 2.2 2.5.2.5 2.5-2 1.5.3 2.7 2.2 1-.5 2.5-2 1.5-2-1-2.3 1-2.2-2.5-.5-.5-2.5 2-1.5-.5-2.5-2-1.5 1-2.3-1.5-2.2-2.5-.2-.5-2.5 2-1.5-.3-2.7-2.2-1 .5-2.5 2-1.5z')

def section_line(g):
    line(g,6,5,6,27,accent=True); poly(g,[(4,8),(6,5),(8,8)],accent=True); poly(g,[(4,24),(6,27),(8,24)],accent=True)
    rect(g,11,7,16,18,1); path(g,'M11 19h6l4-5 6 4')

def sheet_manager(g):
    rect(g,8,4,17,22,1); rect(g,5,7,17,22,1,accent=True)
    line(g,9,12,18,12); line(g,9,16,18,16); line(g,9,20,15,20)

def text_modifier(g):
    line(g,6,8,19,8); line(g,6,13,19,13); line(g,6,18,15,18)
    path(g,'M14 25l8-8 3 3-8 8-4 1z',accent=True); line(g,21,18,24,21,accent=True)

def visibility_copier(g):
    path(g,'M3 15s5-7 13-7 13 7 13 7-5 7-13 7S3 15 3 15z')
    circle(g,16,15,3,accent=True)
    rect(g,19,19,8,8,1,accent=True); line(g,17,17,23,17); line(g,17,17,17,23)

icons={
'align_line':align_line,'align_text':align_text,'batch_task':batch_task,'boolean_geometry':boolean_geometry,
'cn_check':cn_check,'column_along_path':column_path,'dim_override':dim_override,
'distribute_text':distribute_text,'entity_generator':loft,'solid_extract':solid_extract,'face_based':face_based,'family_instance':family_instance,
'family_manager':family_manager,'level_modifier':level_modifier,'linear_placement':linear_placement,'merge_text':merge_text,
'micro_tool':micro_tool,'override_cleaner':override_cleaner,'paste_cad':paste_cad,'project_info':project_info,
'project_inspector':project_inspector,'parameter_cleanup':parameter_cleanup,'sample_window':settings,'section_line':section_line,'sheet_manager':sheet_manager,
'text_modifier':text_modifier,'visibility_copier':visibility_copier,'cad_to_revit':cad_to_revit,
'rebar_gradient_set':rebar_gradient_set,
}

# Remove existing icon assets; write only the currently referenced set.
for p in ROOT.iterdir():
    if p.is_file(): p.unlink()
for name,draw in icons.items(): icon(name,draw)
print('generated',len(icons),'icon families')
