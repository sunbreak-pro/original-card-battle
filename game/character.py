# =============================================================================
# character.py - キャラクター（プレイヤーと敵）の定義
# =============================================================================
#
# このファイルでは、ゲームに登場するキャラクターを定義しています。
# プレイヤー（Player）と敵（Enemy）の両方が含まれます。
#
# 【このファイルで学べること】
# - クラスの継承と多態性（ポリモーフィズム）
# - 属性（プロパティ）の管理
# - メソッドの実装
#
# =============================================================================

# randomモジュールをインポート
# 乱数生成に使用します（敵の行動決定、ランダム敵生成など）
import random


# =============================================================================
# Character クラス - キャラクターの基本クラス
# =============================================================================
class Character:
    """
    プレイヤーと敵の両方に共通する基本機能を持つクラスです。
    
    【解説】
    このクラスは「抽象的な」キャラクターを表現しています。
    ゲームでは直接このクラスを使わず、Player や Enemy といった
    具体的なクラスを使用します。
    
    共通機能をここにまとめることで：
    1. コードの重複を避けられる
    2. 一箇所を修正すれば全てに反映される
    3. 新しい種類のキャラクターを追加しやすい
    """
    
    def __init__(self, name, max_hp):
        """
        キャラクターのコンストラクタ
        
        【引数の説明】
        - name: キャラクターの名前
        - max_hp: 最大HP（ヒットポイント、体力）
        
        【実行内容】
        キャラクターの基本的な属性（名前、HP、ブロック）を初期化します。
        """
        # 名前を保存
        self.name = name
        
        # 最大HPを保存
        # 最大HPは回復時の上限として使用します
        self.max_hp = max_hp
        
        # 現在のHPを最大HPで初期化
        # ゲーム中にダメージを受けたり回復したりすると変化します
        self.current_hp = max_hp
        
        # ブロック（シールド）を0で初期化
        # ブロックは受けるダメージを軽減します
        # 通常、ターン終了時にリセットされます
        self.block = 0
    
    def receive_damage(self, damage):
        """
        ダメージを受けるメソッド
        
        【引数の説明】
        - damage: 受けるダメージ量
        
        【戻り値】
        - int: 実際に受けたダメージ量（ブロックを考慮した後の値）
        
        【実行内容】
        1. まずブロックでダメージを軽減
        2. 残りのダメージをHPから減らす
        3. 実際に受けたダメージを返す
        
        【ダメージ計算の仕組み】
        例：ブロック5、ダメージ8の場合
        1. ブロック5でダメージ5を吸収
        2. 残りのダメージ3がHPに適用される
        3. ブロックは0になる
        
        例：ブロック10、ダメージ8の場合
        1. ブロック8でダメージ8を全て吸収
        2. HPへのダメージは0
        3. ブロックは2残る
        """
        # 実際のダメージ計算
        # ブロックがダメージより大きい場合、実際のダメージは0
        # min() は小さい方の値を返す関数です
        actual_damage = max(0, damage - self.block)
        
        # ブロックの更新
        # ブロックからダメージ分を引く（0未満にはならない）
        # max() は大きい方の値を返す関数です
        self.block = max(0, self.block - damage)
        
        # HPを減らす
        self.current_hp -= actual_damage
        
        # HPが0未満にならないようにする
        # 0未満になると表示がおかしくなるため
        if self.current_hp < 0:
            self.current_hp = 0
        
        return actual_damage
    
    def heal(self, amount):
        """
        HPを回復するメソッド
        
        【引数の説明】
        - amount: 回復量
        
        【戻り値】
        - int: 実際に回復した量
        
        【実行内容】
        1. HPを回復する
        2. ただし最大HPを超えないようにする
        3. 実際の回復量を返す
        """
        # 回復前のHPを記録
        old_hp = self.current_hp
        
        # HPを回復
        self.current_hp += amount
        
        # 最大HPを超えないようにする
        if self.current_hp > self.max_hp:
            self.current_hp = self.max_hp
        
        # 実際に回復した量を計算して返す
        return self.current_hp - old_hp
    
    def gain_block(self, amount):
        """
        ブロックを獲得するメソッド
        
        【引数の説明】
        - amount: 獲得するブロック量
        
        【実行内容】
        現在のブロックに指定量を追加します。
        ブロックは累積されます（重ね掛けできる）。
        """
        self.block += amount
    
    def reset_block(self):
        """
        ブロックをリセットするメソッド
        
        【実行内容】
        ブロックを0にリセットします。
        通常、ターン終了時に呼び出されます。
        
        【解説】
        多くのローグライク系カードゲーム（Slay the Spireなど）では、
        ブロックはターン終了時に消えます。
        これにより、プレイヤーは毎ターン防御について考える必要があります。
        """
        self.block = 0
    
    def is_alive(self):
        """
        生存しているかどうかをチェックするメソッド
        
        【戻り値】
        - bool: 生存している場合はTrue、死亡している場合はFalse
        
        【解説】
        current_hp > 0 という式の結果は True か False になります。
        これを直接 return することで、if文を使わずに済みます。
        """
        return self.current_hp > 0
    
    def get_status(self):
        """
        キャラクターの状態を文字列で返すメソッド
        
        【戻り値】
        - 文字列: キャラクターの現在の状態
        
        【実行内容】
        名前、HP、ブロックを見やすい形式で返します。
        """
        # ブロックがある場合のみブロック情報を表示
        block_info = f" [ブロック:{self.block}]" if self.block > 0 else ""
        return f"{self.name}: HP {self.current_hp}/{self.max_hp}{block_info}"


# =============================================================================
# Player クラス - プレイヤーを表すクラス
# =============================================================================
class Player(Character):
    """
    プレイヤーキャラクターを表すクラスです。
    
    【解説】
    Character クラスを継承しています。
    プレイヤー固有の機能として、以下が追加されています：
    - デッキ（カードの束）
    - 手札
    - 捨て札
    - エネルギー（カードを使うためのリソース）
    - ゴールド（お金）
    """
    
    def __init__(self, name="プレイヤー", max_hp=80):
        """
        プレイヤーのコンストラクタ
        
        【引数の説明】
        - name: プレイヤーの名前（デフォルト: "プレイヤー"）
        - max_hp: 最大HP（デフォルト: 80）
        
        【実行内容】
        1. 親クラス（Character）のコンストラクタを呼び出す
        2. プレイヤー固有の属性を初期化する
        """
        # 親クラスのコンストラクタを呼び出す
        super().__init__(name, max_hp)
        
        # ----- プレイヤー固有の属性 -----
        
        # デッキ（山札）
        # 戦闘開始時やリシャッフル時にここからカードを引きます
        self.deck = []
        
        # 手札
        # 現在使用可能なカードのリスト
        self.hand = []
        
        # 捨て札
        # 使用したカードはここに置かれます
        # デッキが空になったら、捨て札をシャッフルしてデッキに戻します
        self.discard_pile = []
        
        # 最大エネルギー（1ターンに使えるエネルギーの上限）
        self.max_energy = 3
        
        # 現在のエネルギー
        # カードを使用するとエネルギーを消費します
        # ターン開始時にmax_energyにリセットされます
        self.current_energy = self.max_energy
        
        # ゴールド（お金）
        # ショップでカードやアイテムを購入するのに使います
        self.gold = 100
    
    def start_turn(self):
        """
        ターン開始時の処理を行うメソッド
        
        【実行内容】
        1. エネルギーを回復（最大値にリセット）
        2. ブロックをリセット
        
        【解説】
        このメソッドは各ターンの開始時に呼び出されます。
        カードを引く処理は別のメソッド（draw_cards）で行います。
        """
        # エネルギーを最大値にリセット
        self.current_energy = self.max_energy
        
        # ブロックをリセット（0に戻す）
        # 前のターンで獲得したブロックは消えます
        self.reset_block()
    
    def can_use_card(self, card):
        """
        カードを使用できるかどうかをチェックするメソッド
        
        【引数の説明】
        - card: 使用しようとしているカード
        
        【戻り値】
        - bool: 使用できる場合はTrue、できない場合はFalse
        
        【実行内容】
        カードのコストが現在のエネルギー以下かどうかをチェックします。
        """
        return self.current_energy >= card.cost
    
    def use_energy(self, amount):
        """
        エネルギーを消費するメソッド
        
        【引数の説明】
        - amount: 消費するエネルギー量
        
        【実行内容】
        現在のエネルギーから指定量を減らします。
        """
        self.current_energy -= amount
    
    def get_status(self):
        """
        プレイヤーの状態を文字列で返すメソッド（オーバーライド）
        
        【戻り値】
        - 文字列: プレイヤーの現在の状態
        
        【実行内容】
        親クラスの get_status() を呼び出し、エネルギー情報を追加します。
        
        【解説】
        super().get_status() で親クラスのメソッドを呼び出せます。
        その結果に追加情報を付け加えています。
        """
        # 親クラスのget_statusを呼び出して基本情報を取得
        base_status = super().get_status()
        
        # エネルギー情報を追加
        return f"{base_status} | エネルギー: {self.current_energy}/{self.max_energy}"


# =============================================================================
# Enemy クラス - 敵を表すクラス
# =============================================================================
class Enemy(Character):
    """
    敵キャラクターを表すクラスです。
    
    【解説】
    敵はプレイヤーと違い、AIによって自動的に行動します。
    行動パターンはあらかじめ定義された「インテント」に従います。
    
    【インテント（Intent）とは？】
    敵が次のターンに何をするかを示す情報です。
    プレイヤーはインテントを見て、防御するかどうかを判断できます。
    これにより、戦略的なゲームプレイが可能になります。
    """
    
    def __init__(self, name, max_hp, attack_damage, description=""):
        """
        敵のコンストラクタ
        
        【引数の説明】
        - name: 敵の名前
        - max_hp: 最大HP
        - attack_damage: 基本攻撃力
        - description: 敵の説明文（省略可）
        
        【実行内容】
        1. 親クラスのコンストラクタを呼び出す
        2. 敵固有の属性を初期化する
        """
        super().__init__(name, max_hp)
        
        # 敵の基本攻撃力
        self.attack_damage = attack_damage
        
        # 敵の説明文
        self.description = description
        
        # 現在のインテント（次の行動）
        # "attack": 攻撃する
        # "defend": 防御する
        # "buff": 自己強化する
        self.intent = "attack"
        
        # インテントで表示するダメージ量
        # プレイヤーに見せるために保存しておきます
        self.intent_damage = attack_damage
    
    def decide_action(self):
        """
        次の行動を決定するメソッド
        
        【実行内容】
        このメソッドでは、敵の次のターンの行動を決定します。
        基本的な実装では、常に攻撃を選択します。
        
        【解説】
        このメソッドは子クラスでオーバーライドすることで、
        より複雑な行動パターンを実装できます。
        """
        # 基本的な敵は常に攻撃
        self.intent = "attack"
        self.intent_damage = self.attack_damage
    
    def take_action(self, target):
        """
        行動を実行するメソッド
        
        【引数の説明】
        - target: 行動の対象（通常はプレイヤー）
        
        【戻り値】
        - 文字列: 行動結果のメッセージ
        
        【実行内容】
        インテントに応じて適切な行動を実行します。
        """
        if self.intent == "attack":
            # 攻撃の場合
            target.receive_damage(self.attack_damage)
            return f"{self.name}の攻撃！{target.name}に{self.attack_damage}ダメージ！"
        elif self.intent == "defend":
            # 防御の場合
            self.gain_block(5)
            return f"{self.name}は身を守っている。5ブロックを獲得！"
        else:
            # その他の行動
            return f"{self.name}は様子を見ている..."
    
    def get_intent_display(self):
        """
        インテントの表示文字列を返すメソッド
        
        【戻り値】
        - 文字列: インテントを示す絵文字と情報
        
        【実行内容】
        プレイヤーに見せるためのインテント情報を生成します。
        """
        if self.intent == "attack":
            return f"🗡️ 攻撃 ({self.intent_damage}ダメージ)"
        elif self.intent == "defend":
            return f"🛡️ 防御"
        else:
            return f"❓ 不明"
    
    def become_boss(self, hp_multiplier=2, attack_multiplier=1.5):
        """
        敵をボスに強化するメソッド
        
        【引数の説明】
        - hp_multiplier: HPの倍率（デフォルト: 2倍）
        - attack_multiplier: 攻撃力の倍率（デフォルト: 1.5倍）
        
        【実行内容】
        1. 名前に「ボス・」を付ける
        2. HPと攻撃力を強化する
        3. 次の行動を再決定する
        
        【解説】
        このメソッドはカプセル化を維持しながら、敵を強化する方法を提供します。
        外部から直接属性を変更するのではなく、このメソッドを通じて強化します。
        """
        # 名前を変更
        self.name = f"ボス・{self.name}"
        
        # HPを強化
        self.max_hp = int(self.max_hp * hp_multiplier)
        self.current_hp = self.max_hp
        
        # 攻撃力を強化
        self.attack_damage = int(self.attack_damage * attack_multiplier)
        
        # インテントのダメージ値も更新
        if self.intent == "attack":
            self.intent_damage = self.attack_damage


# =============================================================================
# 具体的な敵クラスの定義
# =============================================================================

class Slime(Enemy):
    """
    スライム - 最も基本的な敵
    
    【解説】
    初心者向けの弱い敵です。
    攻撃と防御をランダムに選択します。
    """
    
    def __init__(self):
        """
        スライムのコンストラクタ
        
        【実行内容】
        あらかじめ決められたステータスで Enemy を初期化します。
        """
        super().__init__(
            name="スライム",
            max_hp=20,
            attack_damage=5,
            description="ぷるぷると震える緑色のモンスター"
        )
    
    def decide_action(self):
        """
        スライムの行動決定（オーバーライド）
        
        【実行内容】
        70%の確率で攻撃、30%の確率で防御を選択します。
        
        【解説】
        random モジュールを使って確率的な行動を実現しています。
        （randomはファイル先頭でインポート済み）
        """
        # random.random() は 0.0 以上 1.0 未満の乱数を返します
        if random.random() < 0.7:
            self.intent = "attack"
            self.intent_damage = self.attack_damage
        else:
            self.intent = "defend"
            self.intent_damage = 0


class Goblin(Enemy):
    """
    ゴブリン - 攻撃的な敵
    
    【解説】
    攻撃力が高めの敵です。
    常に攻撃を選択します。
    """
    
    def __init__(self):
        super().__init__(
            name="ゴブリン",
            max_hp=30,
            attack_damage=8,
            description="小柄だが獰猛な緑色の生き物"
        )
    
    def decide_action(self):
        """
        ゴブリンは常に攻撃
        """
        self.intent = "attack"
        self.intent_damage = self.attack_damage


class Skeleton(Enemy):
    """
    スケルトン - バランス型の敵
    
    【解説】
    攻撃と防御をバランスよく選択する敵です。
    """
    
    def __init__(self):
        super().__init__(
            name="スケルトン",
            max_hp=25,
            attack_damage=7,
            description="動く骸骨。どこから来たのだろう..."
        )
        # 行動カウンター（パターン行動に使用）
        self.action_count = 0
    
    def decide_action(self):
        """
        スケルトンは攻撃と防御を交互に行う
        
        【実行内容】
        action_count を使って、攻撃と防御を交互に選択します。
        """
        if self.action_count % 2 == 0:
            # 偶数ターンは攻撃
            self.intent = "attack"
            self.intent_damage = self.attack_damage
        else:
            # 奇数ターンは防御
            self.intent = "defend"
            self.intent_damage = 0
        
        # カウンターを増やす
        self.action_count += 1


# =============================================================================
# 敵を生成するヘルパー関数
# =============================================================================
def create_random_enemy():
    """
    ランダムな敵を生成する関数
    
    【戻り値】
    - Enemy: ランダムに選ばれた敵のインスタンス
    
    【実行内容】
    利用可能な敵の中からランダムに1体を選んで返します。
    （randomはファイル先頭でインポート済み）
    """
    # 敵のクラスをリストにまとめる
    # これはクラスそのものをリストに入れています
    enemy_classes = [Slime, Goblin, Skeleton]
    
    # ランダムに1つ選択
    # random.choice() はリストからランダムに1要素を選びます
    enemy_class = random.choice(enemy_classes)
    
    # 選んだクラスのインスタンスを作成して返す
    # enemy_class() でインスタンスを作成
    return enemy_class()


# =============================================================================
# テスト用コード
# =============================================================================
if __name__ == "__main__":
    print("=== キャラクタークラスのテスト ===")
    
    # プレイヤーを作成
    player = Player("勇者")
    print(f"プレイヤー作成: {player.get_status()}")
    
    # ダメージを受けるテスト
    player.receive_damage(10)
    print(f"10ダメージ後: {player.get_status()}")
    
    # 回復のテスト
    player.heal(5)
    print(f"5回復後: {player.get_status()}")
    
    # ブロックのテスト
    player.gain_block(10)
    print(f"ブロック10獲得: {player.get_status()}")
    
    # ブロック込みでダメージを受けるテスト
    actual = player.receive_damage(15)
    print(f"15ダメージ後(実際:{actual}): {player.get_status()}")
    
    print("\n=== 敵のテスト ===")
    
    # 各敵を作成してテスト
    enemies = [Slime(), Goblin(), Skeleton()]
    for enemy in enemies:
        enemy.decide_action()
        print(f"{enemy.get_status()} - インテント: {enemy.get_intent_display()}")
