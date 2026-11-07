"""`cjforge ref <topic>`: compact, exact parameter reference (the source of truth for recipe keys)."""

from .audio.drums import DRUMS
from .audio.effects import EFFECT_TYPES
from .audio.instruments import PRESETS
from .audio.notation import CHORD_QUALITIES

TOPICS = {
    "overview": """CJForge recipe = JSON file in Tools/CJForge/recipes/<kind>/<name>.json
共通キー: kind(se|bgm|image, 必須)  name(出力名, 既定=ファイル名, '/'でサブフォルダ)  seed(int, 既定1)  _xxx(コメント, 無視)
出力: se->Assets/Temp/Audio/SE/<name>.ogg  bgm->Assets/Temp/Audio/BGM/<name>.ogg(loop) / .mp3(非loop)  image->Assets/Temp/Sprites/Generated/<name>.png
  (mp3 は Unity で先頭25ms無音+末尾余りが入るため、時間精度が要る SE とループ BGM は ogg。形式が変わると旧ファイルは自動削除)
未知のキーはエラー(場所つき)。topics: se bgm melody instruments drums chords notation effects master curve image originality""",

    "originality": """オリジナル性の規則(権利保護, 必ず守る):
- 音は CJForge の合成(波形・ノイズ・数式)だけで作る。録音素材・外部音声ファイル・AI音声生成サービスは使わない(ツールも音の読み込み機能を持たない)
- 既存の曲・作曲者・アーティスト・ゲーム/映画/アニメの音を名前で参照・模倣しない。「〇〇風」「〇〇のような」の指示もジャンル・楽器・テンポ・調・雰囲気の言葉に置き換える
- 既存曲の採譜(耳コピ)・引用・メロディの一部流用をしない
- 旋律は notes:{"generate":{...}} の自動生成を使う(`ref melody`)。手書きの notes は伴奏・ベースライン・短い効果音的フレーズ(4音程度まで)に限る
- コード進行・リズムの型は一般的な語法(ジャンルの共有財産)の範囲で使ってよい""",

    "melody": """旋律の自動生成: track の notes に {"generate":{...}} を書く(手書き文字列の代わり)。スケール・和音・seed だけから作り、既存曲を使わない
generate: key(C|D|Bb|F#..,"C") scale(major|minor|harmonic_minor|dorian|mixolydian|phrygian|pentatonic_major|pentatonic_minor|blues)
  bars(旋律の長さ小節,4。区間内で繰り返す) low("C4") high("C6") density(0.05-1 音の多さ,.5) phrase_bars(2 リズムを繰り返す単位)
  variation(0-1 リズムの変化,.3) max_leap(半音,7) seed(別の旋律にするときに変える)
  chords("Dm9 - - - | ..." 伴奏と同じ進行を書くと拍頭が和音の音になる) chord_step("1/4" chords の1トークンの長さ)
例: {"instrument":"lead","step":"1/8","start_bar":4,"notes":{"generate":{"key":"D","scale":"harmonic_minor","bars":4,"chords":"Dm9 - - - | A7b9 - - -","seed":3}}}""",

    "se": """SE: {"kind":"se","layers":[layer...],"effects":[...],"master":{...},"sample_rate":44100}
layer(音): start(s,0) duration(s,0.2) osc(sine|triangle|saw|square|pulse|noise|pink) freq(curve Hz,440) duty(pulse,0.5)
  env{a,d,s,r}(既定 a.002 d=duration s0 r.05) gain_db(0) pan(-1..1)
  filter{type(lowpass|highpass|bandpass) cutoff(curve Hz) q(.707) slope(12|24)}
  fm{ratio(2) index(2) decay(s,なし=一定)} vibrato{rate(Hz) depth(cents)}
  repeat{count interval(s) pitch_step(半音) gain_step_db}
layer(ドラム): drum(名前, `ref drums`) tune(1) decay(1) tone(.5) + start gain_db pan filter repeat
master 既定: channels=1, trim=true, peak_db=-1""",

    "bgm": """BGM: {"kind":"bgm","tempo":100,"beats_per_bar":4,"bars":8,"loop":true,"swing":0..0.5,
 "instruments":{"名前":{instrument}}, "tracks":[track...], "effects":[...], "master":{...}}
loop=true: 末尾の残響を先頭へ折り返し、ちょうど bars 小節でつながる(.ogg)。結果の loop_click<=1 ならつなぎ目のノイズなし。
track 共通: step("1/8") gain_db pan velocity(倍率1) start_bar end_bar swing gate(.92 音長比) humanize{time(s) velocity} effects[]
 notes  : {"instrument":"x","notes":"C4 . E4 - | [C4 E4 G4]:0.6 .","transpose":0}  旋律は "notes":{"generate":{..}} (`ref melody`, `ref originality`)
 chords : {"instrument":"x","chords":"Cm7 - - - | F7 - - -","center":"C4","style":"block|arp|root","arp":{"step":"1/16","order":"up|down|updown|random","octaves":1}}
 drum   : {"drum":"kick","pattern":"x...x...","tune":1,"decay":1,"tone":.5}
パターンは区間(start_bar..end_bar)の終わりまで繰り返す。記法は `ref notation`。master 既定: channels=2""",

    "notation": """1トークン=1ステップ(step)。'|' は見やすさ用で無視。
notes : C4 Eb3 F#5 / C4:0.6(ベロシティ0..1) / [C4 E4 G4](和音) / .(休符) / -(直前を1ステップ延ばす)
chords: ルート(A-G #/b) + 種類 + 任意の /ベース音。例 Cm7 Bbmaj7 F#m7b5 G7/B .  -
drums : X(強) x(普通) o(弱) .(休符)  例 "x..xx...x...x.o."
step  : "1/4"=4分 "1/8"=8分 "1/16"=16分 "3/8" など(全音符=1)""",

    "effects": """effects: [{"type":...}, ...] 順に適用(se/bgm全体, bgm track)
 reverb{mix(.25) decay(s,1.5) predelay(.01) damping(Hz,6000) width(1)}
 delay{time(s,.25) feedback(.35) mix(.3) pingpong(false)}
 distortion{drive(3) mix(1)}   bitcrush{bits(8) rate(11025)}   tremolo{rate(5) depth(.5)}
 filter{mode(lowpass|highpass|bandpass) cutoff(Hz) q(.707) slope(12|24)}   gain{db}""",

    "master": """master{peak_db(-1) loudness_db(なし|-40..-3: RMS目標+ソフトリミッター) channels(1|2) fade_in(s) fade_out(s,ループ時無効) trim(true: 末尾無音を削る)}""",

    "curve": """curve(時間で変わる値): 数値=一定 / [from,to]=長さ全体で指数グライド / {"points":[[秒,値],...],"curve":"exp|lin"}""",

    "image": """IMAGE: {"kind":"image","size":[w,h],"background":"transparent"|色,"palette":{"gold":"#DEB86A"},"layers":[...]}
色: "#RGB" "#RRGGBB" "#RRGGBBAA" "$palette名" "transparent" "white" "black" [r,g,b,a](0-255)
paint(fill/stroke.color): 色 | {"linear":{"from":[x,y],"to":[x,y]},"stops":[[0,色],[1,色]]} | {"radial":{"center":[x,y],"radius":r},"stops":[...]}
layer 共通: fill(paint) stroke{color(paint) width align(center|inside|outside)} opacity blend(normal|erase|add)
  shadow{color offset[x,y] blur strength} glow{color blur strength}
  変形: rotate(度,時計回り) origin[x,y](既定=中心) offset[x,y] scale
  repeat{count rotate offset[x,y] scale}  (i番目に i倍の変形を足して合成)
  ブーリアン: union[...] subtract[...] intersect[...] (子は shape と変形キーのみ)
shape: circle{center radius} ellipse{center radii[rx,ry]} ring{center radius width}
  rect{rect[x,y,w,h] radius} polygon{points[[x,y]...]} line{from to width cap(round|butt|square)}
  polyline{points width cap} star{center points outer inner angle(-90)} arc{center radius width start end(度,0=右,時計回り)}
  text{text font(serif|mincho|sans|ファイル) size pos[x,y] anchor(mm=中央, la=左上 等 Pillow) rotate line_spacing} (stroke は外側のみ)
  image{path(レシピからの相対) rect[x,y,w,h]}
座標は左上原点・ピクセル単位。結果の touches_edge が出たら画像の端で切れている。""",
}


def topic_text(topic: str) -> str:
    if topic == "instruments":
        lines = ["instrument{preset(下の名前) osc(名前|[{osc semitones cents gain_db duty}]) unison detune(cents) env{a d s r}",
                 " filter{type cutoff q slope env_amount(Hz) key_track(0..1)} fm{ratio index decay} vibrato{rate depth(cents) delay}",
                 " noise{gain_db decay}(アタックのノイズ) gain_db velocity_sens(.7) octave}  preset に上書きキーを足せる",
                 "presets: " + " ".join(PRESETS)]
        return "\n".join(lines)
    if topic == "drums":
        return "drums: " + " ".join(DRUMS) + "\n各ドラム: tune(音程倍率) decay(長さ倍率) tone(0..1 明るさ)"
    if topic == "chords":
        return "chord qualities: " + " ".join(q or "(maj)" for q in CHORD_QUALITIES)
    if topic in TOPICS:
        text = TOPICS[topic]
        if topic == "effects":
            text += "\ntypes: " + " ".join(EFFECT_TYPES)
        return text
    return "topics: " + " ".join(list(TOPICS) + ["instruments", "drums", "chords"])
