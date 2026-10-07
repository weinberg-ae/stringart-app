import re, numpy as np, sys
sys.path.insert(0, 'avatar')
from sdf import preview
s=open('garm/Assets/Resources/PM_Garments/mannequin.dae',encoding='utf-8',errors='ignore').read()
pos=np.array(re.findall(r'<float_array[^>]*positions-array[^>]*>([^<]*)<',s)[0].split(),float).reshape(-1,3)
m=re.findall(r'<matrix[^>]*>([^<]*)<',s)
M=np.array(m[0].split(),float).reshape(4,4) if m else np.eye(4)
P=(np.c_[pos,np.ones(len(pos))]@M.T)[:,:3]
P[:,2]*=-1  # DAE right-handed -> Unity left-handed (z flip)
mn,mx=P.min(0),P.max(0); size=mx-mn
print('size',size)
# stand up: longest axis vertical (same rule as PM_Garment)
if size[2]>size[1] and size[2]>=size[0]: P=P[:,[0,2,1]]*[1,1,-1]
elif size[0]>size[1] and size[0]>size[2]: P=P[:,[1,0,2]]*[-1,1,1]
mn,mx=P.min(0),P.max(0); size=mx-mn
P=P*(1.6/size[1]); mn=P.min(0); mx=P.max(0); c=(mn+mx)/2
P=P-[c[0],mn[1],c[2]]
fb_min=P.min(0); fb_max=P.max(0); fbs=fb_max-fb_min; axis=(fb_min+fb_max)/2
B=24; rx=np.zeros(B); rz=np.zeros(B)
bi=np.clip(((P[:,1]-fb_min[1])/fbs[1]*B).astype(int),0,B-1)
for i in range(B):
    sel=P[bi==i]
    if len(sel): rx[i]=np.abs(sel[:,0]-axis[0]).max(); rz[i]=np.abs(sel[:,2]-axis[2]).max()
def sm(a):
    m=np.array([max(a[max(0,i-1):i+2]) for i in range(len(a))])
    return np.array([m[max(0,i-1):i+2].mean() for i in range(len(m))])
rawx=rx.copy(); rx=sm(rx); rz=sm(rz)
print(np.round(rx,3)); print(np.round(rz,3))
top=B-1
while top>0 and rawx[top]<0.07: top-=1
bottom=top
while bottom>0 and rawx[bottom-1]>0.07: bottom-=1
band=fbs[1]/B
yTop=fb_min[1]+(top+0.6)*band; yT=fb_min[1]+bottom*band
if top-bottom<3: yTop=fb_min[1]+fbs[1]*0.92; yT=fb_min[1]+fbs[1]*0.55
yHem=max(fb_min[1]+0.1,yT-0.38)
rxT=max(0.09,rx[bottom]); rzT=max(0.07,rz[bottom])
print('top',top,'bottom',bottom,'yTop',yTop,'yTorso',yT,'yHem',yHem)
n=40; V=[]; 
for z in range(n+1):
    for x in range(n+1):
        u=x/n; w=z/n; y=yHem+(yTop-yHem)*w
        if y>=yT:
            b=min(max(int((y-fb_min[1])/band),0),B-1)
            ax=max(rx[b],0.06)*1.06+0.012; az=max(rz[b],0.05)*1.06+0.012
            k=np.clip((y-(yTop-0.05))/0.05,0,1); ax=ax+(ax*0.75-ax)*k; az=az+(az*0.7-az)*k
        else:
            f=(yT-y)/max(0.01,yT-yHem); ax=rxT*1.06+0.012+f*0.13; az=rzT*1.06+0.012+f*0.11
        a=u*np.pi*2; V.append([axis[0]+np.cos(a)*ax, y, axis[2]+np.sin(a)*az])
V=np.array(V); F=[]
for z in range(n):
    for x in range(n):
        i=z*(n+1)+x; F+= [[i,i+n+1,i+1],[i+1,i+n+1,i+n+2]]
F=np.array(F)
# mannequin triangles
tris=re.findall(r'<(?:triangles|polylist)[^>]*count="(\d+)"[^>]*>(.*?)</(?:triangles|polylist)>',s,re.S)
mf=[]
for cnt,body in tris:
    inputs=len(re.findall('<input',body)); p=np.array(re.findall(r'<p>([^<]*)</p>',body)[0].split(),int).reshape(-1,inputs)[:,0]
    mf.append(p.reshape(-1,3))
mf=np.concatenate(mf)
allv=np.concatenate([P,V]); allf=np.concatenate([mf,F+len(P)])
col=np.concatenate([np.tile([[0.8,0.8,0.75]],(len(mf),1)),np.tile([[0.5,0.7,0.95]],(len(F),1))])
preview(allv,allf,col,'avatar/out/dress.png',views=((10,-70),(10,20)),title='dress',zoom=2.0)
