"""Writes the DrakesTestLab suite for ItemShop into Tests/suites/DrakesItemShop (run: python gen_itemshop_suite.py).
The build copies it into the test profiles; Tests/ is never packaged."""
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'DrakesItemShop')
GUID = 'com.drakesworkshop.itemshop'
NOERR = {'check': 'noErrors', 'source': 'DrakesItemShop'}


def H(name, expect=None, label=None, contains=None, notContains=None, stop=False, **args):
    s = {'do': 'hook', 'name': 'itemshop.' + name}
    if args:
        s['args'] = args
    for k, v in (('expect', expect), ('contains', contains), ('notContains', notContains), ('label', label)):
        if v is not None:
            s[k] = v
    if stop:
        s['stop'] = True
    return s


def play(who):
    return {'do': 'playAs', 'who': who}


def check(kind, label=None, **f):
    s = {'do': 'check', 'check': kind, **f}
    if label:
        s['label'] = label
    return s


def give(prefab, count=1):
    return {'do': 'give', 'prefab': prefab, 'count': count}


def shop(stock=None, open_=True, piece='Drake_DisplayCase', ward=False, permitted=None):
    """Alice's test shop next to the house: a display case she owns, stocked and priced, open for business."""
    steps = [play('Alice'), {'do': 'spawn', 'prefab': piece, 'dz': 3, 'owner': 'Alice', 'as': 'shop'}, {'do': 'wait', 'seconds': 0.5}]
    if ward:
        w = {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15}
        if permitted:
            w['permitted'] = permitted
        steps.append(w)
    for prefab, count, price, lot in stock or []:
        args = {'prefab': prefab, 'count': count, 'price': price, 'target': 'shop'}
        if lot:
            args['lot'] = lot
        steps.append(H('stock', True, f'stock {count} {prefab} at {price}', stop=True, **args))
    if open_:
        steps.append(H('open', True, 'open for business', stop=True, target='shop', on=True))
    return steps


tests = {}


def T(tid, title, steps, desc=''):
    tests[tid] = {'name': f'{tid} {title}', 'description': desc, 'steps': steps, 'asserts': [NOERR]}


T('S-01', 'build a display case with the hammer', [
    {'do': 'place', 'prefab': 'Drake_DisplayCase', 'dz': 3, 'as': 'shop'},
    H('canManage', True, 'the builder runs the shop', target='shop'),
    H('isOpen', False, 'a new shop starts closed', target='shop'),
    {'do': 'screenshot', 'name': 'display-case'},
], desc='Real placement from the build menu (free).')

T('S-02', 'buy for coins: stock, till, sales log, collect', [
    *shop([('SwordBronze', 1, 'c:20', None)]),
    {'do': 'screenshot', 'name': 'stocked'},
    play('Bob'),
    H('canManage', False, 'a customer cannot run the shop', target='shop'),
    H('buy', "You can't afford that payment.", 'no coins, no sale', target='shop', prefab='SwordBronze'),
    give('Coins', 20),
    H('buy', '', 'Bob buys the sword', stop=True, target='shop', prefab='SwordBronze'),
    check('hasItem', 'the sword is in Bob\'s inventory', prefab='SwordBronze'),
    check('noItem', 'his 20 coins are gone', prefab='Coins'),
    H('stockCount', 0, 'sold out', target='shop', prefab='SwordBronze'),
    H('till', 20, 'the till holds 20 coins', target='shop'),
    H('sales', label='the sale is logged with the buyer', contains='Bob', target='shop'),
    check('message', 'the buyer is told', contains='Bought'),
    H('collect', -1, 'Bob cannot collect the till', target='shop'),
    play('Alice'),
    H('collect', 20, 'Alice collects 20 coins', target='shop'),
    check('hasItem', 'into her inventory', prefab='Coins', count=20),
    H('till', 0, 'till empty', target='shop'),
    check('logContains', 'the shop logged opening', text='open for business = True'),
    play('me'),
])

T('S-03', 'a closed shop does not sell', [
    *shop([('SwordBronze', 1, 'c:20', None)], open_=False),
    play('Bob'),
    give('Coins', 20),
    H('buy', 'The shop is closed.', target='shop', prefab='SwordBronze'),
    check('hasItem', 'coins kept', prefab='Coins', count=20),
    play('me'),
])

T('S-04', 'barter for items', [
    *shop([('SwordBronze', 1, 'Wood:10', None)]),
    play('Bob'),
    give('Wood', 9),
    H('buy', label='9 wood is not enough', notContains='Bought', target='shop', prefab='SwordBronze'),
    give('Wood', 1),
    H('buy', '', 'pays 10 wood', target='shop', prefab='SwordBronze'),
    check('noItem', 'wood handed over', prefab='Wood'),
    H('stockCount', 10, 'the wood is now in the shop', target='shop', prefab='Wood'),
    play('me'),
])

T('S-05', 'alternative payments (coins OR iron)', [
    *shop([('SwordBronze', 2, 'c:50|Iron:2', None)]),
    play('Bob'),
    give('Iron', 2),
    H('buy', '', 'pays with the second option (2 iron)', target='shop', prefab='SwordBronze', option=1),
    check('noItem', 'iron spent', prefab='Iron'),
    give('Coins', 50),
    H('buy', '', 'or with the first (50 coins)', target='shop', prefab='SwordBronze', option=0),
    check('hasItem', 'two swords bought', prefab='SwordBronze', count=2),
    play('me'),
])

T('S-06', 'lots: several items per deal', [
    *shop([('ArrowWood', 20, 'c:3', 5)]),
    play('Bob'),
    give('Coins', 6),
    H('buy', '', '2 deals of 5 arrows', target='shop', prefab='ArrowWood', deals=2),
    check('hasItem', '10 arrows', prefab='ArrowWood', count=10),
    check('noItem', 'paid 2 x 3 coins', prefab='Coins'),
    H('stockCount', 10, '10 left', target='shop', prefab='ArrowWood'),
    play('me'),
])

T('S-07', 'no room in the buyer\'s inventory', [
    *shop([('SwordBronze', 1, 'c:20', None)]),
    play('Bob'),
    give('Coins', 20),
    give('Stone', 1550),
    H('buy', 'No room in your inventory.', target='shop', prefab='SwordBronze'),
    check('hasItem', 'coins not taken', prefab='Coins', count=20),
    H('stockCount', 1, 'still on the shelf', target='shop', prefab='SwordBronze'),
    play('me'),
])

T('S-08', 'sold out and price changed', [
    *shop([('SwordBronze', 1, 'c:20', None)]),
    play('Bob'),
    give('Coins', 100),
    H('buy', label='more than in stock', contains='Sold out', target='shop', prefab='SwordBronze', deals=2),
    play('Alice'),
    H('setPrice', True, 'Alice raises the price', target='shop', prefab='SwordBronze', price='c:30'),
    play('Bob'),
    H('buy', 'The price changed. Look again.', 'buying at the old price is refused', target='shop', prefab='SwordBronze', pay='c:20'),
    H('buy', '', 'at the new price it works', target='shop', prefab='SwordBronze'),
    check('hasItem', 'paid 30, 70 left', prefab='Coins', count=70),
    play('me'),
])

T('S-09', 'coins are not sold for coins', [
    *shop([('Coins', 50, 'c:5', None)]),
    play('Bob'),
    give('Coins', 10),
    H('buy', "Coins aren't sold for coins only.", target='shop', prefab='Coins'),
    play('me'),
])

T('S-10', 'who may run a shop', [
    *shop([('SwordBronze', 1, 'c:20', None)], open_=False, ward=True, permitted=['Carol']),
    play('Carol'),
    H('canManage', True, 'a player permitted on the ward may run it', target='shop'),
    H('open', True, 'and open it', target='shop', on=True),
    play('Bob'),
    H('canManage', False, 'a stranger may not', target='shop'),
    H('setPrice', False, 'or change prices', target='shop', prefab='SwordBronze', price='c:1'),
    H('open', False, 'or close it', target='shop', on=False),
    H('isOpen', True, 'still open', target='shop'),
    play('me'),
])

T('S-11', 'racks and mats are shops too', [
    *shop([('SwordBronze', 1, 'c:5', None)], piece='Drake_WeaponRack'),
    play('Bob'),
    give('Coins', 5),
    H('buy', '', 'buy from a weapon rack', target='shop', prefab='SwordBronze'),
    play('Alice'),
    {'do': 'spawn', 'prefab': 'Drake_DisplayMat', 'dz': 3, 'dx': 2.5, 'owner': 'Alice', 'as': 'mat'},
    {'do': 'wait', 'seconds': 0.5},
    H('stock', True, 'stock a display mat', target='mat', prefab='Club', count=1, price='c:2'),
    H('open', True, target='mat', on=True),
    play('Bob'),
    give('Coins', 2),
    H('buy', '', 'buy from the mat', target='mat', prefab='Club'),
    play('me'),
])

suite = {
    'name': 'DrakesItemShop',
    'description': "Automated regression for Drake's ItemShop: a test shop next to the house (display case, rack, mat) stocked and opened by Alice, "
                   "bought from by Bob through the buy window's own purchase code: coins, barter, alternative payments, lots, refusals, till, "
                   'sales log, collecting and who may run a shop.',
    'requireMods': [{'guid': GUID, 'minVersion': '0.4.0'}],
    'configOverrides': [],
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
print(len(tests), 'ItemShop tests')
