# Input Axis Conflict Resolver Basics

- Negative @100: -1
- Positive @101: 両押下で新しいpositiveが勝ち+1
- Release + @102: negativeへfallbackして-1
- Release All @103: neutral 0
- Both @104: 同一tick edge tieでneutral 0

5 Buttonは960×600で1列、640×360で3+2列に収まります。
