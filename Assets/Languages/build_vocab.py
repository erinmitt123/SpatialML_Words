"""Builds vocabulary.json for Word World from one source table.

Words and phrases are generated from fixed grammar patterns so gender,
plural and article agreement are applied consistently:
  Level 1 (word):     definite article + noun      la silla
  Level 2 (phrase):   indefinite article + noun    una silla   / 一把椅子
  Level 3 (sentence): a hand-written sentence per object (SENTENCES), e.g.
                      Siéntate en la silla, por favor. / 请坐在椅子上。
                      Classes without one fall back to "This is my ___."
"""
import json, sys

# id, coco, en, zh, pinyin, mw, mwPinyin,
# es (article, noun, alt), fr (noun, gender m/f, plural), de (article, noun),
# category, fit (H/M/L)
# es/de plural is implied by article (los/las, German plural flagged with "pl").
T = [
 (1,"person","person","人","rén","个","gè",("la","persona",""),("personne","f",False),("die","Person"),"People","H"),
 (2,"bicycle","bicycle","自行车","zìxíngchē","辆","liàng",("la","bicicleta",""),("vélo","m",False),("das","Fahrrad"),"Vehicles","M"),
 (3,"car","car","汽车","qìchē","辆","liàng",("el","coche","el carro (Latin America)"),("voiture","f",False),("das","Auto"),"Vehicles","L"),
 (4,"motorcycle","motorcycle","摩托车","mótuōchē","辆","liàng",("la","moto",""),("moto","f",False),("das","Motorrad"),"Vehicles","L"),
 (5,"airplane","airplane","飞机","fēijī","架","jià",("el","avión",""),("avion","m",False),("das","Flugzeug"),"Vehicles","L"),
 (6,"bus","bus","公交车","gōngjiāochē","辆","liàng",("el","autobús",""),("bus","m",False),("der","Bus"),"Vehicles","L"),
 (7,"train","train","火车","huǒchē","列","liè",("el","tren",""),("train","m",False),("der","Zug"),"Vehicles","L"),
 (8,"truck","truck","卡车","kǎchē","辆","liàng",("el","camión",""),("camion","m",False),("der","Lastwagen"),"Vehicles","L"),
 (9,"boat","boat","船","chuán","艘","sōu",("el","barco",""),("bateau","m",False),("das","Boot"),"Vehicles","L"),
 (10,"traffic light","traffic light","红绿灯","hónglǜdēng","个","gè",("el","semáforo",""),("feu de circulation","m",False),("die","Ampel"),"Street","L"),
 (11,"fire hydrant","fire hydrant","消防栓","xiāofángshuān","个","gè",("la","boca de incendios",""),("bouche d'incendie","f",False),("der","Hydrant"),"Street","L"),
 (12,"stop sign","stop sign","停车标志","tíngchē biāozhì","个","gè",("la","señal de stop",""),("panneau stop","m",False),("das","Stoppschild"),"Street","L"),
 (13,"parking meter","parking meter","停车计时器","tíngchē jìshíqì","个","gè",("el","parquímetro",""),("parcmètre","m",False),("die","Parkuhr"),"Street","L"),
 (14,"bench","bench","长椅","chángyǐ","张","zhāng",("el","banco",""),("banc","m",False),("die","Bank"),"Furniture","M"),
 (15,"bird","bird","鸟","niǎo","只","zhī",("el","pájaro",""),("oiseau","m",False),("der","Vogel"),"Animals","M"),
 (16,"cat","cat","猫","māo","只","zhī",("el","gato",""),("chat","m",False),("die","Katze"),"Animals","H"),
 (17,"dog","dog","狗","gǒu","只","zhī",("el","perro",""),("chien","m",False),("der","Hund"),"Animals","H"),
 (18,"horse","horse","马","mǎ","匹","pǐ",("el","caballo",""),("cheval","m",False),("das","Pferd"),"Animals","L"),
 (19,"sheep","sheep","羊","yáng","只","zhī",("la","oveja",""),("mouton","m",False),("das","Schaf"),"Animals","L"),
 (20,"cow","cow","牛","niú","头","tóu",("la","vaca",""),("vache","f",False),("die","Kuh"),"Animals","L"),
 (21,"elephant","elephant","大象","dàxiàng","头","tóu",("el","elefante",""),("éléphant","m",False),("der","Elefant"),"Animals","L"),
 (22,"bear","bear","熊","xióng","只","zhī",("el","oso",""),("ours","m",False),("der","Bär"),"Animals","L"),
 (23,"zebra","zebra","斑马","bānmǎ","匹","pǐ",("la","cebra",""),("zèbre","m",False),("das","Zebra"),"Animals","L"),
 (24,"giraffe","giraffe","长颈鹿","chángjǐnglù","只","zhī",("la","jirafa",""),("girafe","f",False),("die","Giraffe"),"Animals","L"),
 (25,"backpack","backpack","背包","bēibāo","个","gè",("la","mochila",""),("sac à dos","m",False),("der","Rucksack"),"Accessories","H"),
 (26,"umbrella","umbrella","雨伞","yǔsǎn","把","bǎ",("el","paraguas",""),("parapluie","m",False),("der","Regenschirm"),"Accessories","H"),
 (27,"handbag","handbag","手提包","shǒutíbāo","个","gè",("el","bolso","la cartera (Latin America)"),("sac à main","m",False),("die","Handtasche"),"Accessories","H"),
 (28,"tie","tie","领带","lǐngdài","条","tiáo",("la","corbata",""),("cravate","f",False),("die","Krawatte"),"Accessories","M"),
 (29,"suitcase","suitcase","行李箱","xínglixiāng","个","gè",("la","maleta",""),("valise","f",False),("der","Koffer"),"Accessories","H"),
 (30,"frisbee","frisbee","飞盘","fēipán","个","gè",("el","frisbi",""),("frisbee","m",False),("die","Frisbeescheibe"),"Sports","M"),
 (31,"skis","skis","滑雪板","huáxuěbǎn","副","fù",("los","esquís",""),("skis","m",True),("pl","Skier"),"Sports","L"),
 (32,"snowboard","snowboard","单板滑雪板","dānbǎn huáxuěbǎn","块","kuài",("la","tabla de snowboard",""),("snowboard","m",False),("das","Snowboard"),"Sports","L"),
 (33,"sports ball","ball","球","qiú","个","gè",("la","pelota",""),("ballon","m",False),("der","Ball"),"Sports","M"),
 (34,"kite","kite","风筝","fēngzheng","只","zhī",("la","cometa","el papalote (Mexico)"),("cerf-volant","m",False),("der","Drachen"),"Sports","L"),
 (35,"baseball bat","baseball bat","棒球棒","bàngqiúbàng","根","gēn",("el","bate de béisbol",""),("batte de baseball","f",False),("der","Baseballschläger"),"Sports","M"),
 (36,"baseball glove","baseball glove","棒球手套","bàngqiú shǒutào","只","zhī",("el","guante de béisbol",""),("gant de baseball","m",False),("der","Baseballhandschuh"),"Sports","M"),
 (37,"skateboard","skateboard","滑板","huábǎn","块","kuài",("el","monopatín",""),("skateboard","m",False),("das","Skateboard"),"Sports","M"),
 (38,"surfboard","surfboard","冲浪板","chōnglàngbǎn","块","kuài",("la","tabla de surf",""),("planche de surf","f",False),("das","Surfbrett"),"Sports","L"),
 (39,"tennis racket","tennis racket","网球拍","wǎngqiúpāi","把","bǎ",("la","raqueta de tenis",""),("raquette de tennis","f",False),("der","Tennisschläger"),"Sports","M"),
 (40,"bottle","bottle","瓶子","píngzi","个","gè",("la","botella",""),("bouteille","f",False),("die","Flasche"),"Kitchen","H"),
 (41,"wine glass","wine glass","酒杯","jiǔbēi","个","gè",("la","copa de vino",""),("verre à vin","m",False),("das","Weinglas"),"Kitchen","H"),
 (42,"cup","cup","杯子","bēizi","个","gè",("la","taza",""),("tasse","f",False),("die","Tasse"),"Kitchen","H"),
 (43,"fork","fork","叉子","chāzi","把","bǎ",("el","tenedor",""),("fourchette","f",False),("die","Gabel"),"Kitchen","H"),
 (44,"knife","knife","刀","dāo","把","bǎ",("el","cuchillo",""),("couteau","m",False),("das","Messer"),"Kitchen","H"),
 (45,"spoon","spoon","勺子","sháozi","把","bǎ",("la","cuchara",""),("cuillère","f",False),("der","Löffel"),"Kitchen","H"),
 (46,"bowl","bowl","碗","wǎn","个","gè",("el","cuenco","el bol"),("bol","m",False),("die","Schüssel"),"Kitchen","H"),
 (47,"banana","banana","香蕉","xiāngjiāo","根","gēn",("el","plátano","la banana (Latin America)"),("banane","f",False),("die","Banane"),"Food","H"),
 (48,"apple","apple","苹果","píngguǒ","个","gè",("la","manzana",""),("pomme","f",False),("der","Apfel"),"Food","H"),
 (49,"sandwich","sandwich","三明治","sānmíngzhì","个","gè",("el","sándwich",""),("sandwich","m",False),("das","Sandwich"),"Food","H"),
 (50,"orange","orange","橙子","chéngzi","个","gè",("la","naranja",""),("orange","f",False),("die","Orange"),"Food","H"),
 (51,"broccoli","broccoli","西兰花","xīlánhuā","棵","kē",("el","brócoli",""),("brocoli","m",False),("der","Brokkoli"),"Food","H"),
 (52,"carrot","carrot","胡萝卜","húluóbo","根","gēn",("la","zanahoria",""),("carotte","f",False),("die","Karotte"),"Food","H"),
 (53,"hot dog","hot dog","热狗","règǒu","个","gè",("el","perrito caliente","el hot dog (Latin America)"),("hot-dog","m",False),("der","Hotdog"),"Food","M"),
 (54,"pizza","pizza","披萨","pīsà","块","kuài",("la","pizza",""),("pizza","f",False),("die","Pizza"),"Food","H"),
 (55,"donut","donut","甜甜圈","tiántiánquān","个","gè",("el","dónut","la dona (Latin America)"),("beignet","m",False),("der","Donut"),"Food","H"),
 (56,"cake","cake","蛋糕","dàngāo","块","kuài",("el","pastel",""),("gâteau","m",False),("der","Kuchen"),"Food","H"),
 (57,"chair","chair","椅子","yǐzi","把","bǎ",("la","silla",""),("chaise","f",False),("der","Stuhl"),"Furniture","H"),
 (58,"couch","couch","沙发","shāfā","张","zhāng",("el","sofá",""),("canapé","m",False),("das","Sofa"),"Furniture","H"),
 (59,"potted plant","plant","盆栽","pénzāi","盆","pén",("la","planta",""),("plante en pot","f",False),("die","Topfpflanze"),"Furniture","H"),
 (60,"bed","bed","床","chuáng","张","zhāng",("la","cama",""),("lit","m",False),("das","Bett"),"Furniture","H"),
 (61,"dining table","table","桌子","zhuōzi","张","zhāng",("la","mesa",""),("table","f",False),("der","Tisch"),"Furniture","H"),
 (62,"toilet","toilet","马桶","mǎtǒng","个","gè",("el","inodoro",""),("toilettes","f",True),("die","Toilette"),"Household","H"),
 (63,"tv","TV","电视","diànshì","台","tái",("la","televisión",""),("télévision","f",False),("der","Fernseher"),"Electronics","H"),
 (64,"laptop","laptop","笔记本电脑","bǐjìběn diànnǎo","台","tái",("el","portátil","la laptop (Latin America)"),("ordinateur portable","m",False),("der","Laptop"),"Electronics","H"),
 (65,"mouse","mouse","鼠标","shǔbiāo","个","gè",("el","ratón","el mouse (Latin America)"),("souris","f",False),("die","Maus"),"Electronics","H"),
 (66,"remote","remote","遥控器","yáokòngqì","个","gè",("el","mando a distancia","el control remoto (Latin America)"),("télécommande","f",False),("die","Fernbedienung"),"Electronics","H"),
 (67,"keyboard","keyboard","键盘","jiànpán","个","gè",("el","teclado",""),("clavier","m",False),("die","Tastatur"),"Electronics","H"),
 (68,"cell phone","phone","手机","shǒujī","部","bù",("el","móvil","el celular (Latin America)"),("téléphone portable","m",False),("das","Handy"),"Electronics","H"),
 (69,"microwave","microwave","微波炉","wēibōlú","台","tái",("el","microondas",""),("micro-ondes","m",False),("die","Mikrowelle"),"Appliances","H"),
 (70,"oven","oven","烤箱","kǎoxiāng","台","tái",("el","horno",""),("four","m",False),("der","Backofen"),"Appliances","H"),
 (71,"toaster","toaster","烤面包机","kǎo miànbāo jī","台","tái",("la","tostadora",""),("grille-pain","m",False),("der","Toaster"),"Appliances","H"),
 (72,"sink","sink","水槽","shuǐcáo","个","gè",("el","fregadero","el lavabo (bathroom sink)"),("évier","m",False),("die","Spüle"),"Household","H"),
 (73,"refrigerator","refrigerator","冰箱","bīngxiāng","台","tái",("la","nevera","el frigorífico"),("réfrigérateur","m",False),("der","Kühlschrank"),"Appliances","H"),
 (74,"book","book","书","shū","本","běn",("el","libro",""),("livre","m",False),("das","Buch"),"Household","H"),
 (75,"clock","clock","钟","zhōng","个","gè",("el","reloj",""),("horloge","f",False),("die","Uhr"),"Household","H"),
 (76,"vase","vase","花瓶","huāpíng","个","gè",("el","jarrón",""),("vase","m",False),("die","Vase"),"Household","H"),
 (77,"scissors","scissors","剪刀","jiǎndāo","把","bǎ",("las","tijeras",""),("ciseaux","m",True),("die","Schere"),"Household","H"),
 (78,"teddy bear","teddy bear","泰迪熊","tàidí xióng","只","zhī",("el","osito de peluche",""),("ours en peluche","m",False),("der","Teddybär"),"Household","H"),
 (79,"hair drier","hair dryer","吹风机","chuīfēngjī","个","gè",("el","secador de pelo",""),("sèche-cheveux","m",False),("der","Föhn"),"Household","H"),
 (80,"toothbrush","toothbrush","牙刷","yáshuā","支","zhī",("el","cepillo de dientes",""),("brosse à dents","f",False),("die","Zahnbürste"),"Household","H"),
]

FIT = {"H": "common", "M": "sometimes", "L": "rare"}
EN_PLURAL = {"skis", "scissors"}
FR_VOWEL = set("aeiouyéèêàâîïôûœ")
FR_H_MUET = {"horloge"}

def cap(s):
    return s[0].upper() + s[1:]

def tone_of(py):
    for ch in py:
        if ch in "āēīōūǖ": return 1
        if ch in "áéíóúǘ": return 2
        if ch in "ǎěǐǒǔǚ": return 3
        if ch in "àèìòùǜ": return 4
    return 5

# ---- English ----
def en_entry(word, zh):
    plural = word in EN_PLURAL
    if plural:
        phrase = f"a pair of {word}"
        sentence = f"These are my {word}."
    else:
        art = "an" if word[0].lower() in "aeiou" else "a"
        phrase = f"{art} {word}"
        sentence = f"This is my {word}."
    return {"word": word, "article": "", "noun": word, "gender": "", "plural": plural,
            "phrase": phrase, "sentence": sentence, "meaning": zh, "alternatives": ""}

# ---- Spanish ----
def es_entry(art, noun, alt, en):
    gender = "m" if art in ("el", "los") else "f"
    plural = art in ("los", "las")
    indef = {"el": "un", "la": "una", "los": "unos", "las": "unas"}[art]
    if plural:
        sentence = f"{'Estos' if gender=='m' else 'Estas'} son mis {noun}."
    else:
        sentence = f"{'Este' if gender=='m' else 'Esta'} es mi {noun}."
    return {"word": f"{art} {noun}", "article": art, "noun": noun, "gender": gender, "plural": plural,
            "phrase": f"{indef} {noun}", "sentence": sentence, "meaning": en, "alternatives": alt}

# ---- French ----
def fr_starts_vowel(noun):
    first = noun.split()[0].lower()
    return first[0] in FR_VOWEL or first in FR_H_MUET

def fr_entry(noun, gender, plural, en):
    v = fr_starts_vowel(noun)
    if plural:
        art, indef, poss = "les", "des", "mes"
        sentence = f"Ce sont mes {noun}."
    else:
        art = "l'" if v else ("le" if gender == "m" else "la")
        indef = "un" if gender == "m" else "une"
        poss = "mon" if (gender == "m" or v) else "ma"
        sentence = f"C'est {poss} {noun}."
    word = f"{art}{noun}" if art == "l'" else f"{art} {noun}"
    return {"word": word, "article": art, "noun": noun, "gender": gender, "plural": plural,
            "phrase": f"{indef} {noun}", "sentence": sentence, "meaning": en, "alternatives": ""}

# ---- German ----
def de_entry(art, noun, en):
    if art == "pl":
        return {"word": f"die {noun}", "article": "die", "noun": noun, "gender": "", "plural": True,
                "phrase": noun, "sentence": f"Das sind meine {noun}.", "meaning": en, "alternatives": ""}
    gender = {"der": "m", "die": "f", "das": "n"}[art]
    indef = "eine" if gender == "f" else "ein"
    poss = "meine" if gender == "f" else "mein"
    return {"word": f"{art} {noun}", "article": art, "noun": noun, "gender": gender, "plural": False,
            "phrase": f"{indef} {noun}", "sentence": f"Das ist {poss} {noun}.", "meaning": en, "alternatives": ""}

# ---- Mandarin ----
def zh_entry(chars, py, mw, mwpy, en):
    yi_py = "yí" if tone_of(mwpy) == 4 else "yì"
    return {"characters": chars, "pinyin": py,
            "measureWord": mw, "measureWordPinyin": mwpy,
            "phrase": f"一{mw}{chars}", "phrasePinyin": f"{yi_py} {mwpy} {py}",
            "sentence": f"这是我的{chars}。", "sentencePinyin": f"Zhè shì wǒ de {py}.",
            "meaning": en}

# Level 3 sentences for the enabled classes, written per object so the sentences vary
# (statements, questions, requests; present, past, imperative). Kept to about 36
# characters so a label wraps to at most two lines. Classes not listed fall back to
# the "This is my ___" pattern above. All of these need native-speaker review.
# coco: (en, es, fr, de, zh, zh pinyin)
SENTENCES = {
 "person": ("There is a person here.", "Hay una persona aquí.", "Il y a une personne ici.", "Hier ist eine Person.", "这里有一个人。", "Zhèlǐ yǒu yí gè rén."),
 "bicycle": ("I ride my bicycle to work.", "Voy al trabajo en bicicleta.", "Je vais au travail à vélo.", "Ich fahre mit dem Fahrrad.", "我骑自行车上班。", "Wǒ qí zìxíngchē shàngbān."),
 "bench": ("We sit on the bench.", "Nos sentamos en el banco.", "On s'assoit sur le banc.", "Wir sitzen auf der Bank.", "我们坐在长椅上。", "Wǒmen zuò zài chángyǐ shàng."),
 "bird": ("The bird is singing.", "El pájaro está cantando.", "L'oiseau chante.", "Der Vogel singt.", "小鸟在唱歌。", "Xiǎoniǎo zài chànggē."),
 "cat": ("The cat is sleeping.", "El gato está durmiendo.", "Le chat dort.", "Die Katze schläft.", "猫在睡觉。", "Māo zài shuìjiào."),
 "dog": ("The dog wants to play.", "El perro quiere jugar.", "Le chien veut jouer.", "Der Hund will spielen.", "狗想出去玩。", "Gǒu xiǎng chūqù wán."),
 "backpack": ("My backpack is heavy.", "Mi mochila pesa mucho.", "Mon sac à dos est lourd.", "Mein Rucksack ist schwer.", "我的背包很重。", "Wǒ de bēibāo hěn zhòng."),
 "umbrella": ("It's raining. Take an umbrella.", "Llueve. Lleva el paraguas.", "Il pleut. Prends le parapluie.", "Es regnet. Nimm den Regenschirm.", "下雨了，带上雨伞。", "Xià yǔ le, dàishàng yǔsǎn."),
 "handbag": ("My keys are in the handbag.", "Mis llaves están en el bolso.", "Mes clés sont dans le sac à main.", "Die Schlüssel sind in der Handtasche.", "我的钥匙在手提包里。", "Wǒ de yàoshi zài shǒutíbāo lǐ."),
 "tie": ("He wears a tie to work.", "Lleva corbata al trabajo.", "Il porte une cravate au travail.", "Er trägt eine Krawatte.", "他上班戴领带。", "Tā shàngbān dài lǐngdài."),
 "suitcase": ("The suitcase is ready.", "La maleta está lista.", "La valise est prête.", "Der Koffer ist gepackt.", "行李箱已经收拾好了。", "Xínglixiāng yǐjīng shōushi hǎo le."),
 "frisbee": ("Throw me the frisbee!", "¡Lánzame el frisbi!", "Lance-moi le frisbee !", "Wirf mir die Frisbeescheibe zu!", "把飞盘扔给我！", "Bǎ fēipán rēng gěi wǒ!"),
 "sports ball": ("Pass me the ball.", "Pásame la pelota.", "Passe-moi le ballon.", "Spiel mir den Ball zu.", "把球传给我。", "Bǎ qiú chuán gěi wǒ."),
 "baseball bat": ("The baseball bat is heavy.", "El bate de béisbol pesa.", "La batte de baseball est lourde.", "Der Baseballschläger ist schwer.", "棒球棒很重。", "Bàngqiúbàng hěn zhòng."),
 "baseball glove": ("My baseball glove is old.", "Mi guante de béisbol es viejo.", "Mon gant de baseball est vieux.", "Mein Baseballhandschuh ist alt.", "我的棒球手套很旧。", "Wǒ de bàngqiú shǒutào hěn jiù."),
 "skateboard": ("She rides a skateboard.", "Ella va en monopatín.", "Elle fait du skateboard.", "Sie fährt Skateboard.", "她在玩滑板。", "Tā zài wán huábǎn."),
 "tennis racket": ("The tennis racket is new.", "La raqueta de tenis es nueva.", "La raquette de tennis est neuve.", "Der Tennisschläger ist neu.", "这把网球拍是新的。", "Zhè bǎ wǎngqiúpāi shì xīn de."),
 "bottle": ("The bottle is empty.", "La botella está vacía.", "La bouteille est vide.", "Die Flasche ist leer.", "瓶子是空的。", "Píngzi shì kōng de."),
 "wine glass": ("The wine glass is full.", "La copa de vino está llena.", "Le verre à vin est plein.", "Das Weinglas ist voll.", "酒杯满了。", "Jiǔbēi mǎn le."),
 "cup": ("I drink coffee from this cup.", "Bebo café en esta taza.", "Je bois du café dans cette tasse.", "Ich trinke Kaffee aus der Tasse.", "我用这个杯子喝咖啡。", "Wǒ yòng zhège bēizi hē kāfēi."),
 "fork": ("I eat with a fork.", "Como con el tenedor.", "Je mange avec une fourchette.", "Ich esse mit der Gabel.", "我用叉子吃饭。", "Wǒ yòng chāzi chīfàn."),
 "knife": ("Be careful, the knife is sharp.", "Cuidado, el cuchillo corta.", "Attention, le couteau coupe.", "Vorsicht, das Messer ist scharf.", "小心，刀很锋利。", "Xiǎoxīn, dāo hěn fēnglì."),
 "spoon": ("I eat soup with a spoon.", "Tomo la sopa con la cuchara.", "Je mange la soupe avec une cuillère.", "Ich esse Suppe mit dem Löffel.", "我用勺子喝汤。", "Wǒ yòng sháozi hē tāng."),
 "bowl": ("The bowl is full of rice.", "El cuenco está lleno de arroz.", "Le bol est plein de riz.", "Die Schüssel ist voller Reis.", "碗里装满了米饭。", "Wǎn lǐ zhuāngmǎn le mǐfàn."),
 "banana": ("The banana is yellow.", "El plátano es amarillo.", "La banane est jaune.", "Die Banane ist gelb.", "香蕉是黄色的。", "Xiāngjiāo shì huángsè de."),
 "apple": ("I eat an apple every day.", "Como una manzana cada día.", "Je mange une pomme chaque jour.", "Ich esse jeden Tag einen Apfel.", "我每天吃一个苹果。", "Wǒ měitiān chī yí gè píngguǒ."),
 "sandwich": ("I'm making a sandwich.", "Estoy haciendo un sándwich.", "Je prépare un sandwich.", "Ich mache ein Sandwich.", "我在做三明治。", "Wǒ zài zuò sānmíngzhì."),
 "orange": ("I peel the orange.", "Pelo la naranja.", "J'épluche l'orange.", "Ich schäle die Orange.", "我在剥橙子。", "Wǒ zài bāo chéngzi."),
 "broccoli": ("Broccoli is good for you.", "El brócoli es sano.", "Le brocoli est bon pour la santé.", "Brokkoli ist gesund.", "西兰花很健康。", "Xīlánhuā hěn jiànkāng."),
 "carrot": ("The rabbit eats a carrot.", "El conejo come una zanahoria.", "Le lapin mange une carotte.", "Der Hase frisst eine Karotte.", "兔子在吃胡萝卜。", "Tùzi zài chī húluóbo."),
 "hot dog": ("A hot dog, please.", "Un perrito caliente, por favor.", "Un hot-dog, s'il vous plaît.", "Einen Hotdog, bitte.", "请给我一个热狗。", "Qǐng gěi wǒ yí gè règǒu."),
 "pizza": ("Let's order a pizza tonight.", "Pidamos una pizza esta noche.", "Commandons une pizza ce soir.", "Bestellen wir heute eine Pizza.", "今晚我们点披萨吧。", "Jīnwǎn wǒmen diǎn pīsà ba."),
 "donut": ("This donut is very sweet.", "Este dónut es muy dulce.", "Ce beignet est très sucré.", "Dieser Donut ist sehr süß.", "这个甜甜圈很甜。", "Zhège tiántiánquān hěn tián."),
 "cake": ("I baked a cake.", "Hice un pastel.", "J'ai fait un gâteau.", "Ich habe einen Kuchen gebacken.", "我烤了一个蛋糕。", "Wǒ kǎo le yí gè dàngāo."),
 "chair": ("Please sit on the chair.", "Siéntate en la silla, por favor.", "Assieds-toi sur la chaise.", "Setz dich auf den Stuhl.", "请坐在椅子上。", "Qǐng zuò zài yǐzi shàng."),
 "couch": ("I relax on the couch.", "Descanso en el sofá.", "Je me repose sur le canapé.", "Ich entspanne mich auf dem Sofa.", "我在沙发上休息。", "Wǒ zài shāfā shàng xiūxi."),
 "potted plant": ("Water the plant every week.", "Riega la planta cada semana.", "Arrose la plante en pot.", "Gieß die Topfpflanze jede Woche.", "每周给盆栽浇水。", "Měi zhōu gěi pénzāi jiāo shuǐ."),
 "bed": ("My bed is very comfortable.", "Mi cama es muy cómoda.", "Mon lit est très confortable.", "Mein Bett ist sehr bequem.", "我的床很舒服。", "Wǒ de chuáng hěn shūfu."),
 "dining table": ("Dinner is on the table.", "La cena está en la mesa.", "Le dîner est sur la table.", "Das Essen steht auf dem Tisch.", "饭菜在桌子上。", "Fàncài zài zhuōzi shàng."),
 "toilet": ("Where is the toilet?", "¿Dónde está el inodoro?", "Où sont les toilettes ?", "Wo ist die Toilette?", "马桶在卫生间里。", "Mǎtǒng zài wèishēngjiān lǐ."),
 "tv": ("The TV is on.", "La televisión está encendida.", "La télévision est allumée.", "Der Fernseher ist an.", "电视开着。", "Diànshì kāizhe."),
 "laptop": ("I work on my laptop.", "Trabajo con mi portátil.", "J'utilise mon ordinateur portable.", "Ich arbeite am Laptop.", "我用笔记本电脑工作。", "Wǒ yòng bǐjìběn diànnǎo gōngzuò."),
 "mouse": ("Click with the mouse.", "Haz clic con el ratón.", "Clique avec la souris.", "Klick mit der Maus.", "用鼠标点击。", "Yòng shǔbiāo diǎnjī."),
 "remote": ("Where is the remote?", "¿Dónde está el mando a distancia?", "Où est la télécommande ?", "Wo ist die Fernbedienung?", "遥控器在哪儿？", "Yáokòngqì zài nǎr?"),
 "keyboard": ("I type on the keyboard.", "Escribo en el teclado.", "Je tape sur le clavier.", "Ich tippe auf der Tastatur.", "我在键盘上打字。", "Wǒ zài jiànpán shàng dǎzì."),
 "cell phone": ("My phone is ringing!", "¡Mi móvil está sonando!", "Mon téléphone portable sonne !", "Mein Handy klingelt!", "我的手机响了！", "Wǒ de shǒujī xiǎng le!"),
 "microwave": ("Heat the food in the microwave.", "Calienta la comida en el microondas.", "Réchauffe le repas au micro-ondes.", "Wärme das Essen in der Mikrowelle.", "用微波炉热饭。", "Yòng wēibōlú rè fàn."),
 "oven": ("The pizza is in the oven.", "La pizza está en el horno.", "La pizza est au four.", "Die Pizza ist im Backofen.", "披萨在烤箱里。", "Pīsà zài kǎoxiāng lǐ."),
 "toaster": ("I put bread in the toaster.", "Pongo pan en la tostadora.", "Je mets du pain dans le grille-pain.", "Ich stecke Brot in den Toaster.", "我把面包放进烤面包机。", "Wǒ bǎ miànbāo fàng jìn kǎo miànbāo jī."),
 "sink": ("Wash the dishes in the sink.", "Lava los platos en el fregadero.", "Lave la vaisselle dans l'évier.", "Spül das Geschirr in der Spüle.", "在水槽里洗碗。", "Zài shuǐcáo lǐ xǐ wǎn."),
 "refrigerator": ("The milk is in the refrigerator.", "La leche está en la nevera.", "Le lait est dans le réfrigérateur.", "Die Milch ist im Kühlschrank.", "牛奶在冰箱里。", "Niúnǎi zài bīngxiāng lǐ."),
 "book": ("I'm reading a good book.", "Estoy leyendo un buen libro.", "Je lis un bon livre.", "Ich lese ein gutes Buch.", "我在看一本好书。", "Wǒ zài kàn yì běn hǎo shū."),
 "clock": ("Look at the clock. It's late.", "Mira el reloj. Es tarde.", "Regarde l'horloge. Il est tard.", "Schau auf die Uhr. Es ist spät.", "看看钟，已经很晚了。", "Kànkan zhōng, yǐjīng hěn wǎn le."),
 "vase": ("Put the flowers in the vase.", "Pon las flores en el jarrón.", "Mets les fleurs dans le vase.", "Stell die Blumen in die Vase.", "把花放进花瓶里。", "Bǎ huā fàng jìn huāpíng lǐ."),
 "scissors": ("Cut the paper with scissors.", "Corta el papel con las tijeras.", "Coupe le papier avec les ciseaux.", "Schneid das Papier mit der Schere.", "用剪刀剪纸。", "Yòng jiǎndāo jiǎn zhǐ."),
 "teddy bear": ("The baby hugs the teddy bear.", "El bebé abraza el osito de peluche.", "Le bébé serre l'ours en peluche.", "Das Baby umarmt den Teddybären.", "宝宝抱着泰迪熊。", "Bǎobao bàozhe tàidí xióng."),
 "hair drier": ("The hair dryer is hot.", "El secador de pelo está caliente.", "Le sèche-cheveux est chaud.", "Der Föhn ist heiß.", "吹风机很热。", "Chuīfēngjī hěn rè."),
 "toothbrush": ("I need a new toothbrush.", "Necesito un cepillo de dientes nuevo.", "Il me faut une brosse à dents.", "Ich brauche eine neue Zahnbürste.", "我需要一支新牙刷。", "Wǒ xūyào yì zhī xīn yáshuā."),
}

entries = []
for (i, coco, en, zh, py, mw, mwpy, es, fr, de, cat, fit) in T:
    e = {
        "classIndex": i - 1,          # YOLOX output index (0-79)
        "cocoLabel": coco,
        "category": cat,
        "roomFit": FIT[fit],
        "enabled": fit != "L",        # rare/outdoor classes are ignored by default
        "reviewed": False,            # set true after native-speaker review
        "en": en_entry(en, zh),
        "zh": zh_entry(zh, py, mw, mwpy, en),
        "es": es_entry(es[0], es[1], es[2], en),
        "fr": fr_entry(fr[0], fr[1], fr[2], en),
        "de": de_entry(de[0], de[1], en),
    }
    if coco in SENTENCES:
        s_en, s_es, s_fr, s_de, s_zh, s_py = SENTENCES[coco]
        e["en"]["sentence"], e["es"]["sentence"] = s_en, s_es
        e["fr"]["sentence"], e["de"]["sentence"] = s_fr, s_de
        e["zh"]["sentence"], e["zh"]["sentencePinyin"] = s_zh, s_py
    entries.append(e)

missing = [e["cocoLabel"] for e in entries if e["enabled"] and e["cocoLabel"] not in SENTENCES]
if missing:
    print("enabled classes using the fallback sentence:", ", ".join(missing))

db = {
    "version": 1,
    "model": "YOLOX-M (COCO 80 classes)",
    "notes": "classIndex matches the YOLOX output index (0-79). Levels: 1 = word, 2 = phrase, 3 = sentence. "
             "Mandarin levels use pinyin -> characters with pinyin -> characters only. "
             "Spanish defaults to Spain; Latin American words are in 'alternatives'. All entries need native-speaker review.",
    "entries": entries,
}

out = sys.argv[1]
with open(out, "w", encoding="utf-8") as f:
    json.dump(db, f, ensure_ascii=False, indent=2)
print(f"wrote {len(entries)} entries, {sum(e['enabled'] for e in entries)} enabled")
