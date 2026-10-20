"""On-screen text for the localized trailers: python make_trailer.py --lang <code> ...

English is the source. The card texts live in make_trailer.CARDS_MAIN (looked up here by "key"),
and the speech-bubble lines come from the takes (`line` events). The footage itself stays as
captured, so every other language:
- replaces the 13 title cards;
- adds a tagline under the logo, because the English title is the punchline of "JUST..." and
  doesn't read as one in other languages;
- subtitles the speech bubbles, which stay in English in the picture.

Chinese, Japanese and Korean use Fusion Pixel Font (12 px, SIL OFL 1.1, fonts/fusion-pixel). It sits
on the same 12-pixel em as the game's own fonts, so it lands on whole pixels at 48/60/96 px.
Turkish uses the game's own LegitPixel/LegitStamp fonts (Tools/ArtGen/fonts.js), which carry the
Turkish letters.

Translations are written for review: keep them short, deadpan and in the same order as English.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
FUSION = os.path.join(HERE, "fonts", "fusion-pixel")

# The English the translations below are keyed on (for reference while reviewing).
ENGLISH_CARDS = {
    "october": "OCTOBER 2008.",
    "sign": "THE SIGN SAYS PASSPORT PHOTOS.",
    "photo": "TAKE THEIR PHOTO.",
    "blink": "PEOPLE BLINK.",
    "papers": "MAKE THEIR PAPERS.",
    "hit": "YOU HIT IT. NOT TOO HARD.",
    "paid": "GET PAID. PROBABLY.",
    "rules": "RULES CHANGE.",
    "word": "WORD GETS AROUND.",
    "studio": "FROM BEDSHEET TO STUDIO.",
    "perfect": "IT DOESN'T HAVE TO BE PERFECT.",
    "just": "JUST...",
    "wishlist": "WISHLIST ON STEAM",
}

LOCALES = {
    # ------------------------------------------------------------------ Simplified Chinese
    "zh-CN": dict(
        name="Simplified Chinese",
        font=os.path.join(FUSION, "fusion-pixel-12px-proportional-zh_hans.otf"),
        cps=12.0,
        cards={
            "october": "2008年10月。",
            "sign": "招牌上写着“证件照”。",
            "photo": "给他们拍照。",
            "blink": "人总会眨眼。",
            "papers": "给他们办证。",
            "hit": "拍一下。别太用力。",
            "paid": "收钱。大概吧。",
            "rules": "规定会变。",
            "word": "口碑传开了。",
            "studio": "从床单到影棚。",
            "perfect": "不必完美。",
            "just": "只要……",
            "wishlist": "加入 Steam 愿望单",
        },
        tagline="够真就行。",
        lines={
            "Is this... the place? A man at the cafe said you could help.": "是……这里吗？咖啡馆有个人说你能帮忙。",
            "I need a French passport.": "我需要一本法国护照。",
            "My wife and daughter are in Lyon. I haven't seen them in two years.": "我妻子和女儿在里昂。我已经两年没见到她们了。",
            "I can't see a thing without them.": "没有眼镜，我什么都看不见。",
            "Sorry.": "抱歉。",
            "Like a statue.": "像雕像一样。",
            "Relaxed. I'm relaxed.": "放松。我很放松。",
            "Hmm.": "嗯……",
            "Wow. Okay.": "哇。好吧。",
            "Here. As agreed.": "给。说好的数。",
            "If anyone asks, I was never here.": "要是有人问起，我从没来过这儿。",
            "Please. You're my last option.": "求你了。你是我最后的希望。",
            "Hi there! Are you... the passport person?": "你好！你就是……办护照的那位？",
            "I can pay $525.": "我能付525美元。",
            "I want to start over. Somewhere nobody knows me.": "我想重新开始。去一个没人认识我的地方。",
        },
    ),
    # ------------------------------------------------------------------ Turkish
    "tr": dict(
        name="Turkish",
        font=None,              # the game's own pixel fonts
        cps=26.0,
        cards={
            "october": "EKİM 2008.",
            "sign": "TABELADA “VESİKALIK FOTOĞRAF” YAZIYOR.",
            "photo": "FOTOĞRAFLARINI ÇEK.",
            "blink": "İNSANLAR GÖZ KIRPAR.",
            "papers": "EVRAKLARINI HAZIRLA.",
            "hit": "BİR TANE VURURSUN. ÇOK SERT DEĞİL.",
            "paid": "PARANI AL. MUHTEMELEN.",
            "rules": "KURALLAR DEĞİŞİR.",
            "word": "ADIN DUYULUR.",
            "studio": "ÇARŞAFTAN STÜDYOYA.",
            "perfect": "MÜKEMMEL OLMASI GEREKMEZ.",
            "just": "SADECE...",
            "wishlist": "STEAM'DE İSTEK LİSTENE EKLE",
        },
        tagline="YETERİNCE SAHİCİ.",
        lines={
            "Is this... the place? A man at the cafe said you could help.": "Burası mı... doğru yer? Kafedeki bir adam yardım edebileceğinizi söyledi.",
            "I need a French passport.": "Fransız pasaportu lazım.",
            "My wife and daughter are in Lyon. I haven't seen them in two years.": "Karım ve kızım Lyon'da. Onları iki yıldır görmedim.",
            "I can't see a thing without them.": "Gözlüksüz hiçbir şey göremem.",
            "Sorry.": "Pardon.",
            "Like a statue.": "Heykel gibi.",
            "Relaxed. I'm relaxed.": "Rahatım. Gerçekten rahatım.",
            "Hmm.": "Hımm.",
            "Wow. Okay.": "Vay. Peki.",
            "Here. As agreed.": "Buyurun. Anlaştığımız gibi.",
            "If anyone asks, I was never here.": "Biri sorarsa, ben hiç burada olmadım.",
            "Please. You're my last option.": "Lütfen. Son çarem sizsiniz.",
            "Hi there! Are you... the passport person?": "Merhaba! Siz... pasaportçu musunuz?",
            "I can pay $525.": "525 dolar ödeyebilirim.",
            "I want to start over. Somewhere nobody knows me.": "Yeniden başlamak istiyorum. Kimsenin beni tanımadığı bir yerde.",
        },
    ),
    # ------------------------------------------------------------------ Japanese
    "ja": dict(
        name="Japanese",
        font=os.path.join(FUSION, "fusion-pixel-12px-proportional-ja.otf"),
        cps=12.0,
        cards={
            "october": "2008年10月。",
            "sign": "看板には「証明写真」とある。",
            "photo": "写真を撮る。",
            "blink": "人はまばたきする。",
            "papers": "書類を作る。",
            "hit": "叩く。強すぎずに。",
            "paid": "金をもらう。たぶん。",
            "rules": "ルールは変わる。",
            "word": "噂は広まる。",
            "studio": "シーツからスタジオへ。",
            "perfect": "完璧じゃなくていい。",
            "just": "ただ……",
            "wishlist": "Steamでウィッシュリストに追加",
        },
        tagline="本物っぽければ、それでいい。",
        lines={
            "Is this... the place? A man at the cafe said you could help.": "ここで……合ってますか？ カフェの男の人が、ここなら助けてくれるって。",
            "I need a French passport.": "フランスのパスポートが要るんです。",
            "My wife and daughter are in Lyon. I haven't seen them in two years.": "妻と娘がリヨンにいるんです。もう二年も会っていません。",
            "I can't see a thing without them.": "メガネがないと、何も見えないんです。",
            "Sorry.": "すみません。",
            "Like a statue.": "銅像みたいに。",
            "Relaxed. I'm relaxed.": "リラックスしてます。ほんとです。",
            "Hmm.": "ふむ……",
            "Wow. Okay.": "わあ。なるほど。",
            "Here. As agreed.": "どうぞ。約束どおりです。",
            "If anyone asks, I was never here.": "誰かに聞かれても、私はここに来ていません。",
            "Please. You're my last option.": "頼む。もう、あんたしかいないんだ。",
            "Hi there! Are you... the passport person?": "こんにちは！ もしかして……パスポートの人？",
            "I can pay $525.": "525ドル払えます。",
            "I want to start over. Somewhere nobody knows me.": "やり直したいんだ。誰も俺を知らない場所で。",
        },
    ),
    # ------------------------------------------------------------------ Korean
    "ko": dict(
        name="Korean",
        font=os.path.join(FUSION, "fusion-pixel-12px-proportional-ko.otf"),
        cps=12.0,
        cards={
            "october": "2008년 10월.",
            "sign": "간판에는 ‘증명사진’이라고 적혀 있다.",
            "photo": "사진을 찍는다.",
            "blink": "사람은 눈을 깜빡인다.",
            "papers": "서류를 만든다.",
            "hit": "한 대 친다. 너무 세게는 말고.",
            "paid": "돈을 받는다. 아마도.",
            "rules": "규칙은 바뀐다.",
            "word": "소문은 퍼진다.",
            "studio": "침대 시트에서 스튜디오까지.",
            "perfect": "완벽할 필요는 없다.",
            "just": "그저……",
            "wishlist": "Steam 찜 목록에 추가하세요",
        },
        tagline="그럴듯하면 충분하다.",
        lines={
            "Is this... the place? A man at the cafe said you could help.": "여기가... 맞나요? 카페에서 어떤 분이 여기서 도와줄 거라고 하던데요.",
            "I need a French passport.": "프랑스 여권이 필요해요.",
            "My wife and daughter are in Lyon. I haven't seen them in two years.": "아내와 딸이 리옹에 있어요. 2년째 못 보고 있어요.",
            "I can't see a thing without them.": "안경 없으면 아무것도 안 보여요.",
            "Sorry.": "죄송해요.",
            "Like a statue.": "동상처럼요.",
            "Relaxed. I'm relaxed.": "편해요. 완전 편해요.",
            "Hmm.": "흠.",
            "Wow. Okay.": "와. 그렇군요.",
            "Here. As agreed.": "여기요. 약속한 대로요.",
            "If anyone asks, I was never here.": "누가 물어보면, 저는 여기 온 적 없는 거예요.",
            "Please. You're my last option.": "제발요. 이제 당신밖에 없어요.",
            "Hi there! Are you... the passport person?": "안녕하세요! 혹시... 여권 해 주시는 분이세요?",
            "I can pay $525.": "525달러 낼 수 있어요.",
            "I want to start over. Somewhere nobody knows me.": "다시 시작하고 싶어요. 아무도 저를 모르는 곳에서.",
        },
    ),
}
