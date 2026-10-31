"""Writes the picture suite for ItemShop into Tests/suites/DrakesItemShop-Pictures (run: python gen_pictures_suite.py).
Not a regression suite: it stocks every shop piece and opens every window to take the README screenshots (docs/images/shop-*.png).
Run: ..\\DrakesTestLab\\tools\\Run-TestLabSuites.ps1 -Suites DrakesItemShop-Pictures -Screenshots Always"""
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'DrakesItemShop-Pictures')
GUID = 'com.drakemods.itemshop'
NOERR = {'check': 'noErrors', 'source': 'DrakesItemShop'}


def H(name, label=None, stop=False, **args):
    s = {'do': 'hook', 'name': name}
    if args:
        s['args'] = args
    if label:
        s['label'] = label
    if stop:
        s['stop'] = True
    return s


def wait(s=0.4):
    return {'do': 'wait', 'seconds': s}


def play(who):
    return {'do': 'playAs', 'who': who}


def shot(name, clean=True):
    return {'do': 'screenshot', 'name': name, 'clean': clean}


def look(tag, distance=2.2):
    return {'do': 'lookAt', 'target': tag, 'distance': distance}


def spawn(prefab, tag='shop', dz=3, dx=0):
    return [{'do': 'spawn', 'prefab': prefab, 'dz': dz, 'dx': dx, 'owner': 'Alice', 'as': tag}, wait(0.5)]


def stock(items, tag='shop', openShop=True):
    steps = [H('itemshop.stock', stop=True, label=f'{p} at {price}', target=tag, prefab=p, count=c, price=price) for p, c, price in items]
    if openShop:
        steps.append(H('itemshop.open', stop=True, target=tag, on=True))
    return steps


tests = {}


def T(tid, title, steps):
    tests[tid] = {'name': f'{tid} {title}', 'description': 'Picture for the README.', 'steps': [H('itemshop.ui.closeAll'), play('Alice'), *steps, H('itemshop.ui.closeAll'), play('me')], 'asserts': [NOERR]}


SWORDS = [('SwordBronze', 1, 'c:40'), ('AxeBronze', 1, 'c:35'), ('ShieldBronzeBuckler', 1, 'c:30'), ('MaceBronze', 1, 'c:45'), ('BowFineWood', 1, 'Iron:4')]
ARMOR = [('HelmetBronze', 1, 'c:30'), ('ArmorBronzeChest', 1, 'c:60'), ('ArmorBronzeLegs', 1, 'c:50')]
GOODS = [('Honey', 4, 'c:3'), ('Coal', 10, 'c:1'), ('Resin', 10, 'c:1'), ('TrophyBoar', 1, 'c:15'), ('Cloudberry', 6, 'c:2'), ('Mushroom', 6, 'c:2')]

# ---- pieces (pictures of what the shop looks like)
T('P-01', 'display case', [*spawn('Drake_DisplayCase'), *stock(GOODS + SWORDS[:2]), look('shop', 2.0), shot('shop-display-case')])
T('P-02', 'small display case', [*spawn('Drake_DisplayCaseSmall'), *stock(GOODS[:3]), look('shop', 1.7), shot('shop-display-case-small')])
T('P-03', 'jute table tray', [*spawn('Drake_DisplayMat'), *stock(SWORDS[:3]), look('shop', 1.9), shot('shop-display-mat')])
T('P-04', 'small jute table tray', [*spawn('Drake_DisplayMatSmall'), *stock(GOODS[:3]), look('shop', 1.6), shot('shop-display-mat-small')])
T('P-05', 'armor rack', [*spawn('Drake_ArmorRack'), *stock(ARMOR), look('shop', 2.5), shot('shop-armor-rack')])
T('P-06', 'small armor rack', [*spawn('Drake_ArmorRackSmall'), *stock(ARMOR[:2]), look('shop', 2.5), shot('shop-armor-rack-small')])
T('P-07', 'weapon rack', [*spawn('Drake_WeaponRack'), *stock(SWORDS), look('shop', 2.5), shot('shop-weapon-rack')])
T('P-08', 'small weapon rack', [*spawn('Drake_WeaponRackSmall'), *stock(SWORDS[:3]), look('shop', 2.5), shot('shop-weapon-rack-small')])
T('P-09', 'wall rack', [*spawn('Drake_WallRack'), *stock([ARMOR[1], SWORDS[0], SWORDS[2], ARMOR[0], SWORDS[1]]), look('shop', 2.5), shot('shop-wall-rack')])
T('P-10', 'small wall rack', [*spawn('Drake_WallRackSmall'), *stock([SWORDS[0], SWORDS[2], ARMOR[0]]), look('shop', 2.5), shot('shop-wall-rack-small')])
T('P-11', 'jute free-layout mat', [*spawn('Drake_JuteMat'), *stock(GOODS[:4] + SWORDS[:2]), look('shop', 2.2), shot('shop-jute-mat')])
T('P-12', 'open crate', [*spawn('Drake_OpenCrate'), look('shop', 1.9), shot('shop-open-crate')])
T('P-13', 'mailbox', [*spawn('Drake_Mailbox'), look('shop', 2.0), shot('shop-mailbox')])
T('P-14', 'drop box', [*spawn('Drake_DropBox'), look('shop', 1.6), shot('shop-drop-box')])
T('P-15', 'store sign', [*spawn('Drake_StoreSign'), H('store.signText', target='shop', text="Drake's Goods"), wait(0.5), look('shop', 2.8), shot('shop-store-sign')])
T('P-16', 'open sign, bells', [*spawn('Drake_OpenSign', 'sign', 3, -0.8), *spawn('Drake_DeskBell', 'bell', 3, 0.6), *spawn('Drake_DoorBell', 'dbell', 3, 1.4), look('sign', 2.0), shot('shop-signs-bells')])
T('P-17', 'trade counter', [*spawn('Drake_TradeCounter'), look('shop', 2.8), shot('shop-trade-counter')])
T('P-18', 'shopkeeper apron', [{'do': 'give', 'prefab': 'Drake_ShopApronMerchant', 'equip': True}, wait(1.0), shot('shop-apron')])

# ---- menus and windows
T('W-01', 'shop menu (Shift+E)', [*spawn('Drake_DisplayCase'), *stock(GOODS[:3] + SWORDS[:2]),
    H('itemshop.ui.shopMenu', target='shop', tab='shop'), wait(0.8), shot('menu-shop', False), H('itemshop.ui.closeAll')])
T('W-02', 'layout tab', [*spawn('Drake_JuteMat'), *stock(GOODS[:4] + SWORDS[:2]),
    H('itemshop.ui.shopMenu', target='shop', tab='layout'), wait(0.8), shot('menu-layout', False), H('itemshop.ui.closeAll')])
T('W-03', 'buy tab', [*spawn('Drake_DisplayCase'), *stock(GOODS[:2]),
    H('itemshop.ui.shopMenu', target='shop', tab='buy'), wait(0.8), shot('menu-buy', False), H('itemshop.ui.closeAll')])
T('W-04', 'price window', [*spawn('Drake_DisplayCase'), *stock([('SwordBronze', 1, 'c:40|Iron:4')]),
    H('itemshop.ui.price', target='shop', prefab='SwordBronze'), wait(0.8), shot('menu-price', False), H('itemshop.ui.closeAll')])
T('W-05', 'buy window', [*spawn('Drake_DisplayCase'), *stock([('ArrowWood', 40, 'c:3|Wood:2')]),
    play('Bob'), {'do': 'give', 'prefab': 'Coins', 'count': 60},
    H('itemshop.ui.buy', target='shop', prefab='ArrowWood'), wait(0.8), shot('menu-buy-window', False), H('itemshop.ui.closeAll')])
T('W-06', 'mailbox send window', [*spawn('Drake_Mailbox'), play('Bob'), {'do': 'give', 'prefab': 'Wood', 'count': 30}, {'do': 'give', 'prefab': 'Honey', 'count': 4},
    H('itemshop.ui.mailSend', target='shop'), wait(0.8), shot('menu-mail-send', False), H('itemshop.ui.closeAll')])
T('W-07', 'trade window', [{'do': 'give', 'prefab': 'SwordBronze'}, {'do': 'give', 'prefab': 'Honey', 'count': 4}, {'do': 'give', 'prefab': 'Coins', 'count': 100},
    H('itemshop.ui.trade', partner='Bjornita', mine='SwordBronze', theirs='Coins:80,Honey:2', theyAccept=True), wait(0.8), shot('menu-trade', False), H('itemshop.ui.closeAll')])

suite = {
    'name': 'DrakesItemShop-Pictures',
    'description': 'Takes the README pictures: every shop piece stocked and every window open. Not a regression suite.',
    'requireMods': [{'guid': GUID, 'minVersion': '0.4.0'}],
    'configOverrides': [],
    'launchConfig': [],
    'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1, 'noBuild': 'off', 'hideUnlockPopups': 1},
    'resetBetweenTests': True,
}

if os.path.isdir(OUT):
    shutil.rmtree(OUT)
os.makedirs(OUT)
with open(os.path.join(OUT, 'suite.json'), 'w', encoding='utf-8') as f:
    json.dump(suite, f, indent=2, ensure_ascii=False)
for tid, t in tests.items():
    with open(os.path.join(OUT, f'{tid}.json'), 'w', encoding='utf-8') as f:
        json.dump(t, f, indent=2, ensure_ascii=False)
print(len(tests), 'picture tests')
