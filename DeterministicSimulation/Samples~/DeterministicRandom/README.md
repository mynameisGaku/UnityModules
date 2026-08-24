# Deterministic Random Basics

`DeterministicRandomBasics.unity`を開いてPlayしてください。

- `Next UInt64`: 固定seedの次の64-bit値
- `Roll D20`: 偏りのない1以上20以下の整数
- `Next Double`: 0以上1未満のdouble
- `Replay State`: 現在stateから6出力し、Reset後の6出力と最終stateを比較
- `Reset Seed`: seed `0xC0FFEE`の初期位置へ戻す

RuntimeはScene、UIDocument、Unity時刻、global乱数を参照しません。このsampleだけがUI Toolkit adapterです。
