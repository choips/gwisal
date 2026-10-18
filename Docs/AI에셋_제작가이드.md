# AI 에셋 제작 가이드 (귀살)

> 2026-10-18 정리. 무엇을 만들지는 `Docs/에셋_준비목록.md`, **어떻게 AI로 만들지**는 이 문서입니다.
> AI 서비스의 이름·가격·사용 조건은 자주 바뀝니다. 가입하기 전에 그 서비스의 **이용 약관(상업적 사용)**을 꼭 확인합니다.
> Claude는 그림·3D를 직접 만들지 못합니다. 프롬프트 작성, 결과물 검토(기획에 맞는지), Blender·Unity 손질 안내를 합니다.

---

## 1. 무엇을 AI로, 무엇을 다른 방법으로

| 에셋 | 방법 | 쓸 만한 정도 |
|---|---|---|
| 컨셉 그림 (분위기를 정하는 그림) | 그림 AI | ⭐⭐⭐ **가장 먼저** |
| 아이템 아이콘 · UI(한지 패널·붓 획 바·낙관) | 그림 AI | ⭐⭐⭐ |
| 한지 질감 · 먹 번짐 산 배경 · 이펙트 문양 | 그림 AI | ⭐⭐⭐ |
| 음악 (국악풍 3곡) · 환경음 | 음악 AI | ⭐⭐⭐ |
| 효과음 12개 | 효과음 AI | ⭐⭐ |
| 움직이지 않는 소품 (장승·서낭당·장독·석등·바위·석탑) | 그림 AI → 그림→3D AI | ⭐⭐ 한국 소품을 구하는 가장 좋은 방법 |
| 플레이어 · NPC · 사람 모양 귀신(망자·창귀) | 그림 AI → 그림→3D AI → **Mixamo**(뼈대·동작) | ⭐⭐ 손질이 조금 필요 |
| 지역 조각 (20m 길·벽·담장·건물) | Unity **ProBuilder**(무료, Unity 안에 있음)로 단순한 모양 + AI로 만든 질감 | AI 3D는 치수를 못 맞춤 |
| 산군 (호랑이) · 네발 귀신 | **동작이 들어 있는 에셋 구매** 검토 | Mixamo는 사람 모양만 됨 |
| 이펙트 움직임 | Unity 파티클 + AI로 만든 문양 그림 | Cursor가 만듦 |

**도구 예시** (2026-10 기준, 바뀔 수 있음)
- 그림 AI: ChatGPT 그림 생성, Midjourney, Gemini 등
- 그림→3D AI: Meshy, Tripo, Hunyuan3D 등
- 음악 AI: Suno 등 / 효과음 AI: ElevenLabs 효과음 등
- 무료 도구: **Mixamo**(뼈대·동작), **Blender**(3D 손질), Unity **ProBuilder**

---

## 2. 사용 조건 — 출시할 게임에 넣기 전에 꼭

- [ ] 그 서비스가 **상업적 사용**을 허락하는지. 무료 요금제 결과물은 상업적으로 못 쓰는 곳이 많습니다 → 게임에 넣을 것은 **허락되는 요금제에서** 만듭니다.
- [ ] 만든 것을 **기록**합니다 (맨 아래 "6. 만든 에셋 기록"): 도구 · 요금제 · 날짜 · 프롬프트. 나중에 문제가 생겼을 때의 증거입니다.
- [ ] **Steam**: AI로 만든 콘텐츠가 들어가면 출시 신청 때 그 사실을 밝혀야 합니다 (금지 아님, 표시만). 기록이 있으면 쉽게 적을 수 있습니다.
- [ ] 다른 게임·작품의 이름이나 그림(예: 오카미 스크린샷)을 **프롬프트에 넣지 않습니다.** 비슷하게 베낀 결과가 나올 수 있습니다. 대신 아래 스타일 문장을 씁니다.
- 연습·시험용(연습 프로젝트에서만 쓰는 것)은 무료 요금제로 만들어도 됩니다. 게임에 넣을 때 허락되는 요금제로 다시 만듭니다.

---

## 3. 귀살 스타일 문장 (모든 그림 프롬프트에 붙임)

> AI는 그릴 때마다 느낌이 달라집니다. **같은 스타일 문장을 매번 그대로 붙이는 것**이 통일감의 핵심입니다. 영어로 넣는 쪽이 대체로 결과가 안정적입니다.

**스타일 (영어 — 복사해서 붙임)**
```
Korean ink wash painting style (sumukhwa) with light watercolor tints, hanji paper texture, variable-width black brush outlines, flat colors with soft watercolor gradients, muted palette of ink black, grays and warm paper beige, accent colors only in red, blue and yellow, calm dark misty mood, Joseon-era Korean motifs
```

**빼고 싶은 것 (영어 — 뒤에 붙임)**
```
no Japanese elements, no torii gate, no Japanese architecture, no anime style, no photorealism, no glossy 3D render, no text, no watermark, no signature
```

**한국어 뜻** (확인용): 한국 수묵화에 옅은 수채 색, 한지 질감, 굵기가 변하는 먹 외곽선, 평평한 색과 부드러운 번짐, 먹·회색·한지색 위주에 강조색은 빨강·파랑·노랑만, 차분하고 어두운 안개 분위기, 조선 시대 한국 소재. 일본풍·애니풍·사진 같은 그림·3D 렌더·글자는 빼기.

- 마음에 드는 그림이 나오면, 다음부터는 그 그림을 **참고 그림으로 함께 넣고** "같은 스타일로"라고 덧붙이면 더 잘 맞습니다 (서비스가 지원하면).
- 만든 컨셉 그림 중 기준으로 삼을 것은 `Docs/ArtReference/`에 넣고 README에 적습니다 (Claude가 정리).

---

## 4. 에셋별 프롬프트 예시

> 형식: **[무엇을] + [모양 조건] + 스타일 문장 + 빼고 싶은 것**. `[ ]` 안은 바꿔서 씁니다.

### ① 컨셉 그림 (⭐ 가장 먼저, 3~4장)
```
Isometric top-down game screenshot, 45-degree quarter view camera, a young Korean Taoist exorcist in a red robe throwing paper talismans at gray undead ghosts in the courtyard of a ruined Korean Buddhist temple at night, a red ink splash explosion on the ground, fog, [스타일 문장], [빼고 싶은 것]
```
- 바꿔 볼 장면: `a small Korean thatched-roof village at the foot of a mountain` (마을) / `a huge corrupted mountain tiger spirit in a dark cave, gaunt orange ghost servants around it` (산군) / `a misty pine forest mountain path with stone steps and a wooden jangseung totem` (산길)
- 볼 점: 수묵 느낌이 나는지, 한국풍인지, 쿼터뷰에서 캐릭터가 잘 보이는지.

### ② 아이템 아이콘
```
Single game item icon of [a yellow paper talisman with red brush calligraphy], centered, plain hanji paper background, simple brush painting, bold silhouette, square 1:1, [스타일 문장], [빼고 싶은 것]
```
- `[ ]` 예: `a small round medicine pill in a ceramic jar` (물약) · `a Korean brass coin with a square hole` (엽전) · `a worn cloth Taoist robe` · `a wooden prayer bead bracelet`
- 아이콘은 **모두 같은 배경·같은 크기 비율**로 만듭니다. Unity에서는 256×256으로 줄여 씁니다.

### ③ UI
```
Game UI panel made of torn hanji paper with brush stroke border, empty center, front view, [스타일 문장], [빼고 싶은 것]
```
- 다른 것: `horizontal brush stroke health bar, red ink` · `red square Korean seal stamp (nakgwan) with carved characters` · `ink pot shaped resource orb, empty and full versions`
- 배경을 투명하게 해 달라고 해도 잘 안 될 때가 많습니다 → 배경 지우기 도구(그림 AI 자체 기능 또는 무료 배경 제거 사이트)로 지웁니다.

### ④ 질감 · 배경
```
Seamless tileable texture of [Korean hanji mulberry paper with subtle fibers], flat lighting, top view, no objects
```
- 다른 것: `[ink wash ground with soft gray stains]` (바닥) · `[weathered roof tiles]` · `[old wooden planks]` (ProBuilder 벽·바닥용)
- 먼 배경: `Wide panorama of layered distant mountains in Korean ink wash, fading into mist, [스타일 문장]`

### ⑤ 3D로 바꿀 소품 그림 (그림 → 3D AI에 넣을 그림)
```
Single [Korean wooden jangseung guardian totem pole], full object visible, centered, plain white background, three-quarter front view, simple chunky shapes, flat colors, no shadows, no ground, game asset concept, [스타일 문장]
```
- `[ ]` 예: `Korean seonangdang sacred tree with five-colored cloth strips and a stone pile` · `Korean earthenware kimchi jar (jangdok)` · `Korean stone lantern` · `small Korean stone pagoda` · `mossy boulder`
- 3D AI는 **배경이 깨끗하고, 물체 하나만, 전체가 보이는** 그림에서 가장 잘 됩니다.

### ⑥ 3D로 바꿀 캐릭터 그림
```
Full body character concept of [a young Korean Taoist exorcist in a red hanbok-style robe], standing in A-pose, arms slightly away from the body, front view, plain white background, simple shapes, flat colors, no weapon, game character concept, [스타일 문장]
```
- `[ ]` 예: `a gray emaciated undead villager in torn hanbok` (망자) · `a thin hunched orange ghost with long arms` (창귀) · `an old Korean village chief in white hanbok` (촌장) · `a peddler carrying a wooden A-frame backpack (jige)` (보부상)
- **A자 자세(팔을 몸에서 조금 벌린 자세)**여야 Mixamo가 뼈대를 잘 넣습니다. 손에 무기·소품을 들리지 않습니다.

### ⑦ 음악 (음악 AI)
```
Korean traditional instrumental, daegeum flute and haegeum, slow and calm but dark, misty mountain at night, ambient game background music, seamless loop, no vocals
```
- 마을: `gayageum, gentle and warm, peaceful Korean village, no vocals`
- 보스: `janggu and buk drums, fast and tense, Korean traditional percussion, boss battle, no vocals`
- 음악은 **1~2분짜리**로, 끝과 처음이 자연스럽게 이어지는지 들어 봅니다.

### ⑧ 효과음 (효과음 AI)
| 효과음 | 넣을 문장 (영어) |
|---|---|
| 부적 던지기 | `quick paper whoosh, a talisman thrown through the air` |
| 부적 적중 | `soft paper slap with a short magical crackle` |
| 부적 폭발 | `deep magical burst with ink splash, short` |
| 플레이어 피격 | `dull cloth hit, short grunt-less impact` |
| 귀신 사망 | `ghostly sigh fading into wind` |
| 아이템 떨어짐 | `small object dropping on dirt` (레어 이상: `bright chime with item drop`) |
| 엽전 줍기 | `brass coins jingling, short` |
| 레벨업 | `rising wooden chime and soft gong` |
| UI 클릭 | `soft wooden click` |
- 쪽부적·호신부·제웅은 스킬 모양이 정해지면 문장을 함께 정합니다.

---

## 5. 3D 손질 순서 (AI 3D → 게임)

> 처음 할 때는 Claude에게 화면을 설명하거나 캡처를 보내면 단계별로 안내합니다.

**소품**
1. 그림→3D AI에 ⑤의 그림을 넣고 모델을 만듭니다. **FBX**(없으면 GLB)로 받습니다.
2. **Blender**(무료)로 엽니다 → 삼각형 수 줄이기(**Decimate** 기능) — 소품 500~2,000개, 큰 소품 3,000개 이하.
3. 크기 맞추기: Unity 1칸 = 1m. 사람 키 1.7m 기준으로 장승 2~2.5m, 장독 0.6m 정도.
4. FBX로 내보내기 → **연습 프로젝트**에 넣어 쿼터뷰로 확인.

**캐릭터 (사람 모양)**
1. ⑥의 그림으로 3D 모델을 만듭니다 → Blender에서 삼각형 수 줄이기 (플레이어 8천, 귀신 3천 이하).
2. **Mixamo**(mixamo.com, Adobe 계정 무료)에 FBX를 올림 → 턱·손목·팔꿈치·무릎·사타구니 위치에 점 찍기 → 자동 뼈대.
3. 필요한 동작을 골라 받기: 서기 · 달리기 · 던지기 · 주문 · 맞기 · 죽기 (+ 귀신: 걷기·공격).
4. 연습 프로젝트에서 Humanoid로 설정해 움직여 봅니다.

**확인** — `Docs/에셋_준비목록.md` "4. 에셋마다 확인할 것"과 같음 (URP·삼각형·텍스처·한국풍).

---

## 6. 만든 에셋 기록

> 게임에 넣을 것은 빠짐없이 적습니다 (사용 조건 증거, Steam 표시용). 사용자가 휴대폰 메모로 보내 주면 Claude가 적습니다.

| 날짜 | 에셋 | 도구 | 요금제 (상업 사용 가능?) | 프롬프트 (요약) | 파일 | 게임에 넣음? |
|---|---|---|---|---|---|---|
| | | | | | | |
