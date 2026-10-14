# Home Run Derby

Unityで制作中のホームランダービーです。投球の方向探索、飛翔中の空気抵抗と回転、打撃時のIK補正、打球の軌道計算を実装しています。

## 実装内容

- **投球**: 球種は`PitchTypeData`（ScriptableObject）で管理します。球速と回転設定を固定し、`PitchVelocitySolver`が指定した通過点へ向かう投球方向を探します。飛翔中は空気抵抗、マグヌス力、重力を計算します。
- **打撃**: スイングのアニメーションを基に腰・腕・手首を補正し、バットの芯がカーソル位置を通るようにします。接触時刻のボール位置と速度を使って打球を計算します。
- **ゲーム進行**: 投球開始、リリース、着弾などをイベントで通知します。`HomeRunDerbyManager`が打席、スコア、結果表示、次の投球を進めます。

投球・打球の速度は計算内部でm/s、回転数は球種設定でrpmを使います。球場の縮尺は`BaseballWorldScale`で**1m = 1.2 Unity単位**に換算しています。縫い目による空力の変化は現在のモデルに含めていません。

## 確認方法

Unity **6000.0.32f1** でプロジェクトを開き、`Assets/Scenes/HomeRun.unity`を再生します。投球後、マウスで打撃位置を指定し、左クリックでスイングします。

打撃画面の小さな水色の点と白い輪は、表示を見て次のフレームでスイングした場合に、接触姿勢へ達する時刻のボール位置をカーソルと同じ2D画面上に示したものです。外側の黄色い輪が白い輪に重なる時刻がジャスト入力です。その時点で位置表示は打撃面の通過点に止まり、短時間だけ緑、その後は赤で遅れを示します。ヒット判定には、クリック時のカーソル位置と接触時のボール位置の距離を使います。

投球の軌道だけを比較する場合は`Assets/Scenes/PitchTest.unity`を使います。操作とコース指定・方向指定の違いは[投球テストシーンの説明](Assets/Script/Pitch/Testing/README.md)に記載しています。

## 主なコード

| 処理 | ファイル |
| --- | --- |
| 投球方向の探索 | `Assets/Script/Physics/PitchVelocitySolver.cs` |
| 飛翔と接地 | `Assets/Script/Physics/BallTrajectorySimulator.cs` |
| 打球の計算 | `Assets/Script/Batting/BattingCalculator.cs`、`BattingPhysics.cs` |
| スイングの補正 | `Assets/Script/Batting/BatSwingAimIK.cs` |
| ボール位置と入力時刻の表示 | `Assets/Script/UI/BattingBallTimingIndicator.cs` |
| 打席の進行 | `Assets/Script/GameSystem/HomeRunDerbyManager.cs` |

## 使用技術・アセット

- Unity 6 / C#
- [UniTask](https://github.com/Cysharp/UniTask)
- [DOTween](https://dotween.demigiant.com/)
- [AudioManager](https://github.com/kankikuchi/AudioManager)
- Final IK（有料アセット）

## 作品動画

[プレイ動画](https://www.youtube.com/watch?v=AzPuaAeeBLI&t=2s)
