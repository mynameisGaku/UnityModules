# Generational Handle Basics

Sceneを開いて、`Acquire A`、`Acquire B`、`Release A`、`Reacquire`、`Reject Stale A`の順に押してください。

最初のhandle `0:1` を解放すると、再割当は最小の空きslot `0` をgeneration `2`として返します。最後に古い `0:1` を解放しようとしても `StaleHandle` となり、現在の `0:2` は有効なままです。
