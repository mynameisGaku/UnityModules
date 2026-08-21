# Weighted Integer Allocator Basics

整数総量とentry weight列を実Buttonで配分する設定済みSceneです。

1. `Equal`は10 unitを同weightの3 entryへ4・3・3で配ります。
2. `Weighted`は12 unitをweight 1・2・3へ2・4・6で配ります。
3. `Remainder`は8 unitをweight 5・3・2へ4・2・2で配り、最大剰余の3番目へ追加1 unitを渡します。
4. `Zero weight`は5 unitをweight 0・4へ0・5で配ります。
5. `Zero total`はtotal 0・全weight 0を成功として0・0で返します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
