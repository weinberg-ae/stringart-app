import math
from PIL import Image, ImageDraw
S=4; W,H=256,512
INK=(22,22,34,240)
def canvas():
    im=Image.new('RGBA',(W*S,H*S),(0,0,0,0)); return im, ImageDraw.Draw(im)
def P(x,y): return (x*S,y*S)
def line(d,pts,w): 
    d.line([P(*p) for p in pts],fill=INK,width=int(w*S),joint='curve')
    for p in (pts[0],pts[-1]): r=w*S/2; d.ellipse([p[0]*S-r,p[1]*S-r,p[0]*S+r,p[1]*S+r],fill=INK)
def curve(f,t0,t1,n=200): return [f(t0+(t1-t0)*i/n) for i in range(n+1)]
def save(im,name):
    im=im.resize((W,H),Image.LANCZOS); im.save(name+'.png')

# 1 needle and thread
im,d=canvas()
# needle: from (150,60) to (100,330)
a=(150,70); b=(104,330)
dx,dy=b[0]-a[0],b[1]-a[1]; L=math.hypot(dx,dy); ux,uy=dx/L,dy/L; nx,ny=-uy,ux
poly=[]
for t,wd in [(0,7),(0.08,8),(0.85,5),(1,0)]:
    poly.append((a[0]+dx*t+nx*wd, a[1]+dy*t+ny*wd))
for t,wd in reversed([(0,7),(0.08,8),(0.85,5),(1,0)]):
    poly.append((a[0]+dx*t-nx*wd, a[1]+dy*t-ny*wd))
d.polygon([P(*p) for p in poly],fill=INK)
# eye (hole)
ex,ey=a[0]+dx*0.07,a[1]+dy*0.07
d.ellipse([P(ex-2.5,ey-9)[0],P(ex-2.5,ey-9)[1],P(ex+2.5,ey+9)[0],P(ex+2.5,ey+9)[1]],fill=(0,0,0,0))
# thread: through the eye, loop down into a heart
def thread(t):
    # parametric heart around (128,380) after a swirl
    if t<1:
        x=ex+ (t*60)*math.cos(t*5) ; y=ey+ t*120 + 40*math.sin(t*4)
        return (x,y)
    s=(t-1)*2*math.pi
    hx=16*math.sin(s)**3; hy=-(13*math.cos(s)-5*math.cos(2*s)-2*math.cos(3*s)-math.cos(4*s))
    return (128+hx*4.2, 400+hy*4.2)
def th(t):
    x0,y0=ex,ey; x1,y1=128,379
    x=x0+(x1-x0)*t + 38*math.sin(t*math.pi*2.2)*(1-t*0.3)
    y=y0+(y1-y0)*t
    return (x,y)
line(d,curve(th,0,1,200),4)
line(d,curve(thread,1,2,300),5)
save(im,'gtattoo_needle')

# 2 rose
im,d=canvas()
cx,cy=128,130
for k in range(5):
    r=18+k*14
    st=k*0.9
    pts=curve(lambda t:(cx+(r+t*6)*math.cos(t+st), cy+(r*0.85+t*5)*math.sin(t+st)),0,4.2,120)
    line(d,pts,5)
# outer petals
for sgn in (-1,1):
    pts=curve(lambda t:(cx+sgn*(70+10*math.sin(t*3))*math.cos(t), cy+20+60*math.sin(t)),-0.3,1.6,100)
    line(d,pts,5)
# stem
stem=curve(lambda t:(128+18*math.sin(t*3.5), 205+t*270),0,1,120)
line(d,stem,6)
# thorns
for t in (0.25,0.5,0.75):
    x,y=128+18*math.sin(t*3.5),205+t*270; sg=1 if t!=0.5 else -1
    d.polygon([P(x,y-8),P(x+sg*16,y-14),P(x,y+4)],fill=INK)
# leaves
for t,sg in ((0.38,-1),(0.62,1)):
    x,y=128+18*math.sin(t*3.5),205+t*270
    up=[(x+sg*u*70, y-28*math.sin(u*math.pi)-u*30) for u in [i/40 for i in range(41)]]
    dn=[(x+sg*u*70, y+18*math.sin(u*math.pi)-u*30) for u in [i/40 for i in range(41)]]
    d.polygon([P(*p) for p in up+dn[::-1]],fill=INK)
    line(d,[(x,y),(x+sg*60,y-28)],2)
save(im,'gtattoo_rose')

# 3 geometric
im,d=canvas()
line(d,[(128,40),(128,470)],3)
for i,y in enumerate(range(90,450,72)):
    s=34 if i%2==0 else 24
    d.polygon([P(128,y-s),P(128+s,y),P(128,y+s),P(128-s,y)],outline=INK,width=4*S)
    if i%2==0: d.polygon([P(128,y-s*0.45),P(128+s*0.45,y),P(128,y+s*0.45),P(128-s*0.45,y)],fill=INK)
    for dx in (-58,58): r=5; d.ellipse([P(128+dx-r,y-r)[0],P(128+dx-r,y-r)[1],P(128+dx+r,y+r)[0],P(128+dx+r,y+r)[1]],fill=INK)
for y in (40,470):
    d.polygon([P(128,y-12),P(140,y),P(128,y+12),P(116,y)],fill=INK)
save(im,'gtattoo_geo')

# 4 iron with steam
im,d=canvas()
# iron outline (side view), sole at y=330
sole=[(50,330),(206,330)]
body=[(50,330),(70,270),(130,240),(206,250),(214,300),(206,330)]
line(d,body+[(50,330)],7)
line(d,[(62,345),(214,345)],5)   # sole plate
# handle
line(d,curve(lambda t:(100+90*t, 240-60*math.sin(t*math.pi)),0,1,60),7)
# temperature dial
r=12; d.ellipse([P(150-r,285-r)[0],P(150-r,285-r)[1],P(150+r,285+r)[0],P(150+r,285+r)[1]],outline=INK,width=4*S)
# steam curls
for k,x0 in enumerate((80,128,176)):
    pts=curve(lambda t:(x0+12*math.sin(t*2*math.pi*1.2+k), 220-60-t*120),0,1,80)
    line(d,pts,4)
# sparkles
for (x,y,s) in ((60,90,14),(200,70,10),(128,40,8)):
    d.polygon([P(x,y-s),P(x+s*0.3,y),P(x,y+s),P(x-s*0.3,y)],fill=INK)
    d.polygon([P(x-s,y),P(x,y-s*0.3),P(x+s,y),P(x,y+s*0.3)],fill=INK)
save(im,'gtattoo_iron')
