import numpy as np, struct, sys
from sdf import preview, rot
def load(path):
    b=open(path,'rb').read(); o=0
    m,vc,sc,nc,jc=struct.unpack_from('<5i',b,o); o+=20
    P=[];J=[];A=[]
    for i in range(jc):
        p,x,y,z,ax,ay,az=struct.unpack_from('<i3f3f',b,o); o+=28; P.append(p); J.append((x,y,z)); A.append((ax,ay,az))
    v=np.frombuffer(b,'<f4',vc*3,o).reshape(-1,3); o+=vc*12; o+=vc*12; o+=vc*8
    I=np.frombuffer(b,'<i4',vc*2,o).reshape(-1,2); o+=vc*8
    W=np.frombuffer(b,'<f4',vc*2,o).reshape(-1,2); o+=vc*8
    sk=np.frombuffer(b,'<i4',sc,o).reshape(-1,3); o+=sc*4
    nl=np.frombuffer(b,'<i4',nc,o).reshape(-1,3)
    return v,I,W,sk,nl,P,np.array(J),np.array(A)
def rotm(axis,deg):
    a=np.asarray(axis,float); a/=np.linalg.norm(a); t=np.radians(deg); K=np.array([[0,-a[2],a[1]],[a[2],0,-a[0]],[-a[1],a[0],0]])
    return np.eye(3)+np.sin(t)*K+(1-np.cos(t))*K@K
def pose(v,I,W,P,J,A,g,t):
    n=len(J); ang=np.zeros(n)
    maxA=[62,92,60]
    for f in range(1,5):
        c=t if f==1 else g
        for j in range(3): ang[1+f*3+j]=maxA[j]*c
    ang[2]=18*g; ang[3]=28*g
    # world transform of each joint: R (3x3) and T
    R=[None]*n; T=[None]*n
    for i in range(n):
        Rl=rotm(A[i],ang[i]) if ang[i]!=0 else np.eye(3)
        if P[i]<0: R[i]=Rl; T[i]=J[i]
        else:
            p=P[i]; R[i]=R[p]@Rl; T[i]=T[p]+R[p]@(J[i]-J[p])
    out=np.zeros_like(v)
    for k in range(2):
        b=I[:,k]; w=W[:,k][:,None]
        Rb=np.stack([R[x] for x in range(n)]); Tb=np.stack([T[x] for x in range(n)])
        local=v-J[b]
        out+=w*(np.einsum('nij,nj->ni',Rb[b],local)+Tb[b])
    return out
v,I,W,sk,nl,P,J,A=load('out/hand_%s.bytes'%sys.argv[1])
f=np.concatenate([sk,nl]); col=np.tile([[0.93,0.76,0.64]],(len(f),1)); col[len(sk):]=[0.9,0.3,0.4]
for name,g,t in (('half',0.5,0.3),('fist',1,1)):
    pv=pose(v,I,W,P,J,A,g,t)
    preview(pv,f,col,'out/pose_%s.png'%name,views=((30,-40),(-20,150)),title=name)
