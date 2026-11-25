"""Original program-drawn fixtures, never presented as generated photo pets."""
import io
from PIL import Image,ImageDraw


def cat(size=512):
    image=Image.new('RGBA',(256,256));d=ImageDraw.Draw(image)
    d.ellipse((40,72,218,232),fill='#c0a386');d.ellipse((65,35,194,151),fill='#dcc9b3')
    d.polygon([(66,70),(70,16),(110,48)],fill='#9b795d');d.polygon([(154,47),(192,16),(194,74)],fill='#9b795d')
    d.ellipse((82,77,101,92),fill='#548da7');d.ellipse((155,77,174,92),fill='#548da7')
    d.polygon([(120,104),(136,104),(128,115)],fill='#bc8887')
    d.ellipse((78,207,128,233),fill='#eee0cf');d.ellipse((132,207,182,233),fill='#eee0cf')
    return image.resize((size,size))


def png(image):
    out=io.BytesIO();image.save(out,'PNG');return out.getvalue()


def gif(base, mode='good'):
    frames=[]
    for i in range(16):
        image=base.copy()
        if i not in (0,15) and mode!='static':
            d=ImageDraw.Draw(image);amount=min(i,15-i)
            d.rectangle((105,155,145,155+amount*5),fill='#6b4d38')
        if mode=='floating' and i==8:
            shifted=Image.new('RGBA',image.size);shifted.alpha_composite(image,(0,-24));image=shifted
        if mode=='bad_end' and i==15:
            shifted=Image.new('RGBA',image.size);shifted.alpha_composite(image,(20,0));image=shifted
        alpha=image.getchannel('A');palette=image.convert('RGB').quantize(colors=255)
        palette.paste(255,mask=alpha.point(lambda a:255 if a<96 else 0))
        palette.info['transparency']=255;frames.append(palette)
    out=io.BytesIO();frames[0].save(out,'GIF',save_all=True,append_images=frames[1:],duration=120,loop=0,disposal=2,optimize=False,transparency=255)
    return out.getvalue()


class FakeProvider:
    image_model='fixture-images';review_model='fixture-review';base_url='https://fixture.invalid/v1'
    def __init__(self,key='',**kwargs):self.calls=[];self.closed=False
    def preflight(self):self.calls.append('preflight')
    def edit(self,refs,prompt,*,animated=False):
        self.calls.append('gif' if animated else 'png')
        if not animated:return png(cat())
        return gif(Image.open(io.BytesIO(refs[0])).convert('RGBA').resize((256,256)))
    def review(self,images,instruction):self.calls.append('review');return {'passed':True,'reason':'synthetic integration fixture'}
    def close(self):self.closed=True
