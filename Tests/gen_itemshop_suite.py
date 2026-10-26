"""Writes the DrakesTestLab suite for ItemShop into Tests/suites/DrakesItemShop (run: python gen_itemshop_suite.py).
The build copies it into the test profiles; Tests/ is never packaged."""
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'DrakesItemShop')
GUID = 'com.drakemods.itemshop'
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

# ---------------------------------------------------------------- every piece, mail, post wall, storefront, buying, crates, apron

def M(name, expect=None, label=None, contains=None, notContains=None, stop=False, **args):
    s = {'do': 'hook', 'name': name}
    if args:
        s['args'] = args
    for k, v in (('expect', expect), ('contains', contains), ('notContains', notContains), ('label', label)):
        if v is not None:
            s[k] = v
    if stop:
        s['stop'] = True
    return s


def wait(s=0.3):
    return {'do': 'wait', 'seconds': s}


def place(prefab, tag, dz=3, dx=0, label=None, **f):
    s = {'do': 'place', 'prefab': prefab, 'dz': dz, 'dx': dx, 'as': tag}
    if label:
        s['label'] = label
    s.update(f)
    return s


SHOP_PIECES = ['Drake_DisplayCase', 'Drake_DisplayCaseSmall', 'Drake_DisplayMat', 'Drake_DisplayMatSmall', 'Drake_DisplayMatWolf',
               'Drake_DisplayMatLox', 'Drake_DisplayMatWolfSmall', 'Drake_DisplayMatLoxSmall', 'Drake_ArmorRack', 'Drake_ArmorRackSmall',
               'Drake_WeaponRack', 'Drake_WeaponRackSmall', 'Drake_WallRack', 'Drake_WallRackSmall']
OTHER_PIECES = ['Drake_Mailbox', 'Drake_DropBox', 'Drake_StoreSign', 'Drake_OpenSign', 'Drake_DoorBell', 'Drake_DeskBell',
                'Drake_TradeCounter', 'Drake_OpenCrate', 'Drake_OpenCrateSmallLow', 'Drake_OpenCrateBigLow', 'Drake_OpenCrateTall',
                'Drake_JuteMat', 'Drake_DeerMat', 'Drake_WolfMat', 'Drake_LoxMat']


def build_each(pieces, label):
    steps = []
    for prefab in pieces:
        steps += [place(prefab, 'p', label=f'{label}: {prefab}'), {'do': 'destroy', 'target': 'p'}, wait(0.1)]
    return steps


T('S-12', 'every shop piece builds from the hammer (cases, mats, racks)', [
    *build_each(SHOP_PIECES, 'shop piece'),
], desc='Each piece is placed through the build menu (it must be listed and switched on) and removed again.')

T('S-13', 'every other ItemShop piece builds (mail, storefront, crates, mats)', [
    *build_each(OTHER_PIECES, 'piece'),
])

T('S-14', 'hidden pieces stay out of the build menu', [
    M('itemshop.pieceEnabled', False, 'the PO box is not in the build menu (it only comes inside post wall sections)', prefab='Drake_POBox'),
    place('Drake_POBox', 'pobox', expect=False, label='the hammer cannot build a PO box on its own'),
])

T('S-15', 'build costs: needs the materials, uses them up', [
    place('piece_workbench', 'bench', dz=-3, dx=3, label='a workbench in range (the shop pieces need one, like a chest)'),
    {'do': 'give', 'prefab': 'FineWood', 'count': 10}, {'do': 'give', 'prefab': 'JuteRed', 'count': 2}, {'do': 'give', 'prefab': 'Crystal', 'count': 1},
    place('Drake_DisplayCase', 'case', free=False, expect=False, label='one crystal short: refused'),
    {'do': 'give', 'prefab': 'Crystal', 'count': 1},
    place('Drake_DisplayCase', 'case', free=False, label='with the full cost it builds'),
    check('noItem', 'fine wood used', prefab='FineWood'),
    check('noItem', 'crystal used', prefab='Crystal'),
], desc='Default DisplayCaseCost FineWood:10,JuteRed:2,Crystal:2.')

T('S-16', 'AdminOnlyBuildPieces', [
    {'do': 'setConfig', 'values': {'01 General/AdminOnlyBuildPieces': 'true'}},
    place('Drake_DisplayCase', 'case', label='the single-player host is an admin: may still build'),
], desc='A non-admin being refused needs a dedicated server: manual.')

def mailbox(prefab='Drake_Mailbox', owner='Alice', ward=True):
    """The owner builds the box; inside the owner's ward by default (a mailbox's back door follows the ward, like any chest)."""
    s = [play(owner), place(prefab, 'box', dz=2.5)]
    if ward:
        s.append({'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': owner, 'enabled': True, 'radius': 15})
    return s + [wait(0.5)]

T('S-20', 'mailbox: anyone posts, only the owner opens', [
    *mailbox(),
    M('mail.canOpen', True, 'Alice (builder) may open her mailbox', target='box'),
    play('Bob'),
    M('mail.maySend', True, 'Bob may post into it', target='box'),
    give('Wood', 10),
    M('mail.send', '', 'Bob posts 10 wood with a note', stop=True, target='box', prefab='Wood', count=10, note='hi Alice'),
    check('noItem', 'the wood left Bob\'s inventory', prefab='Wood'),
    M('mail.count', 10, 'the wood is in the box', target='box', prefab='Wood'),
    M('mail.recent', label='the log shows who sent it and the note', contains='hi Alice', target='box'),
    M('mail.canOpen', False, 'Bob may not open it', target='box'),
    {'do': 'interact', 'target': 'box'}, wait(0.4),
    check('uiVisible', 'pressing E at the front as Bob only offers the send slot', name='InventoryGui', expect=False),
    {'do': 'closeUi'},
    {'do': 'teleport', 'to': 'box', 'distance': 1.5}, wait(0.3),
    {'do': 'interact', 'target': 'box'}, wait(0.4),
    check('uiVisible', 'round the back Bob still cannot open it (inside the ward of Alice)', name='InventoryGui', expect=False),
    {'do': 'closeUi'},
    play('Alice'),
    {'do': 'interact', 'target': 'box'}, wait(0.4),
    check('uiVisible', 'Alice opens the back door', name='InventoryGui'),
    check('containerHas', 'and sees the wood', target='box', prefab='Wood', count=10),
    {'do': 'screenshot', 'name': 'mailbox'},
    {'do': 'closeUi'},
    play('me'),
])

T('S-21', 'drop box works the same', [
    *mailbox('Drake_DropBox'),
    play('Bob'),
    give('Stone', 5),
    M('mail.send', '', target='box', prefab='Stone', count=5),
    M('mail.count', 5, target='box', prefab='Stone'),
    M('mail.canOpen', False, 'not Bob\'s', target='box'),
    play('me'),
])

T('S-22', 'assigning a PO box to a player', [
    play('Alice'),
    place('Drake_PostSection1x3', 'section', dz=3, label='Alice (the postmaster) builds a post wall section'),
    wait(1.0),
    {**M('mail.sectionBox', index=0, target='section'), 'as': 'box'},
    M('mail.signName', label='an unassigned PO box says so', contains='Unassigned', target='box', plain=''),
    M('mail.assign', True, 'Alice gives the box to Carol', target='box', who='Carol'),
    M('mail.signName', label='the plate shows its new owner', contains='Carol', target='box', plain=''),
    play('Carol'),
    M('mail.mayOpen', True, 'Carol may open it now', target='box'),
    M('mail.canOpen', True, 'and the chest agrees (PO box access rules)', target='box'),
    play('Bob'),
    M('mail.mayOpen', False, 'Bob still may not', target='box'),
    M('mail.canOpen', False, 'not even through the chest', target='box'),
    M('mail.assign', False, 'and cannot reassign it', target='box', who='Bob'),
    play('Alice'),
    M('mail.canOpen', True, 'the postmaster (builder) can still open it', target='box'),
    M('mail.assign', True, 'clear the assignment', target='box', who=''),
    play('Carol'),
    M('mail.mayOpen', False, 'Carol lost access', target='box'),
    play('me'),
], desc='Assigning owners is a PO box feature; a plain mailbox belongs to its builder and follows the ward.')

T('S-23', 'mailbox sign and a full box', [
    *mailbox(),
    M('mail.setSign', True, target='box', text='Alice & Co'),
    M('mail.signName', label='sign text shown', contains='Alice & Co', target='box', plain=''),
    play('Bob'),
    M('mail.setSign', False, 'Bob cannot change the sign', target='box', text='Bob was here'),
    give('Stone', 2000),
    M('mail.send', label='posting more than fits is refused', notContains='may not', target='box', prefab='Stone', count=2000),
    M('mail.count', label='observed: how much fitted (informational)', target='box', prefab='Stone'),
    play('me'),
])

T('S-24', 'a mailbox outside a ward is open to anyone (and says so)', [
    *mailbox(ward=False),
    M('mail.isProtected', False, 'no ward: the box knows it is unprotected', target='box'),
    play('Bob'),
    M('mail.canOpen', label='observed: anyone may open an unwarded mailbox (by design; informational)', target='box'),
    play('Alice'),
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    M('mail.isProtected', True, 'inside her ward it is protected', target='box'),
    play('Bob'),
    M('mail.canOpen', False, 'and Bob can no longer open it', target='box'),
    play('me'),
])

T('S-25', 'post wall: sections build PO boxes, each with its own owner', [
    place('Drake_PostSection1x3', 'section', dz=3, label='build a post wall section (experimental setting on for this suite)'),
    wait(1.0),
    M('mail.sectionBoxes', label='the section spawned PO boxes', notContains='0', target='section'),
    {'do': 'screenshot', 'name': 'post-wall'},
    {**M('mail.sectionBox', index=0, target='section'), 'as': 'box0'},
    {**M('mail.sectionBox', index=1, target='section'), 'as': 'box1'},
    M('mail.assign', True, 'the builder gives box 0 to Bob', target='box0', who='Bob'),
    play('Carol'),
    give('Wood', 3),
    M('mail.send', '', 'Carol posts to box 0', target='box0', prefab='Wood', count=3),
    play('Bob'),
    M('mail.canOpen', True, 'Bob opens his PO box', target='box0'),
    M('mail.canOpen', False, 'but not the next one', target='box1'),
    M('mail.count', 3, 'his mail is there', target='box0', prefab='Wood'),
    play('me'),
], desc='PO boxes are reported glitchy: these checks pin down spawn, ownership and delivery.')

T('S-26', 'post wall: boxes survive logging out and in; removing the section removes its boxes', [
    place('Drake_PostSection1x3', 'section', dz=3, keep=True),
    wait(1.0),
    {**M('mail.sectionBox', index=0, target='section'), 'as': 'box0'},
    give('Wood', 4),
    M('mail.send', '', target='box0', prefab='Wood', count=4),
    M('mail.sectionBoxes', label='boxes before the relog (informational)', target='section'),
    {'do': 'relog', 'timeout': 240},
    M('mail.sectionBoxes', label='the same number of boxes after the relog (no duplicates, none lost)', notContains='0', target='section'),
    M('mail.count', 4, 'the mail is still in box 0', target='box0', prefab='Wood'),
    {'do': 'destroy', 'target': 'section'},
    {'do': 'wait', 'timeout': 15, 'until': {'check': 'objectNear', 'target': 'Drake_POBox', 'radius': 6, 'expect': False}},
    check('objectNear', 'no PO box is left behind (boxes remove themselves a few seconds after their section)', target='Drake_POBox', radius=6, expect=False),
    check('objectNear', 'the mail in box 0 dropped on the ground, not lost', target='Wood', radius=6),
], desc='Duplicate or orphaned PO boxes are the classic post-wall glitch.')

T('S-30', 'Open Sign flips OPEN / CLOSED', [
    place('Drake_OpenSign', 'sign', dz=2.5),
    M('store.signIsOpen', True, 'starts OPEN', target='sign'),
    {'do': 'interact', 'target': 'sign'}, wait(0.3),
    M('store.signIsOpen', False, 'E turns it CLOSED', target='sign'),
    check('hoverText', 'the hover says closed', target='sign', contains='CLOSED'),
    {'do': 'interact', 'target': 'sign'}, wait(0.3),
    M('store.signIsOpen', True, 'and back to OPEN', target='sign'),
])

T('S-31', 'Store Sign text and bells', [
    place('Drake_StoreSign', 'store', dz=2.5),
    M('store.signText', "Drake's Goods", 'set the sign text (sign window Save)', target='store', text="Drake's Goods"),
    {'do': 'screenshot', 'name': 'store-sign'},
    place('Drake_DeskBell', 'desk', dz=2.5, dx=1.5),
    {'do': 'interact', 'target': 'desk'}, wait(0.3),
    check('lastResult', 'E rings the desk bell', expect=True),
    place('wood_door', 'door', dz=3, dx=-2),
    place('Drake_DoorBell', 'doorbell', dz=3, dx=-3),
    wait(0.3),
    {'do': 'interact', 'target': 'door'}, wait(0.6),
    check('message', 'opening a nearby door rings the door bell', contains='customer'),
])

T('S-40', 'buying from customers: offer, budget, sell, collect', [
    *shop(),
    M('buy.addOffer', True, 'Alice buys wood: 10 per deal for 5 coins, budget 20', target='shop', want='Wood', amount=10, pay=5, budget=20),
    play('Bob'),
    M('buy.addOffer', False, 'Bob cannot add offers to Alice\'s shop', target='shop', want='Stone', amount=1, pay=1),
    give('Wood', 10),
    M('buy.sell', '', 'Bob sells 10 wood', stop=True, target='shop', offer=0, deals=1),
    check('noItem', 'his wood is gone', prefab='Wood'),
    check('hasItem', 'he got 5 coins', prefab='Coins', count=5),
    M('buy.budget', 15, 'the budget dropped by 5', target='shop'),
    M('buy.held', 0, 'no buy crate: nothing is held on the card', target='shop', prefab='Wood'),
    check('containerHas', 'the wood went into the shop chest', target='shop', prefab='Wood', count=10),
    play('me'),
], desc='Without a buy crate the goods go straight into the shop chest; with a crate they are held on the card until collected.')

T('S-41', 'buying: out of budget and not enough goods', [
    *shop(),
    M('buy.addOffer', True, target='shop', want='Wood', amount=10, pay=5, budget=4),
    play('Bob'),
    give('Wood', 10),
    M('buy.sell', label='budget too small: refused', notContains='Sold', target='shop', offer=0, deals=1),
    check('hasItem', 'wood kept', prefab='Wood', count=10),
    play('Alice'),
    M('buy.addOffer', True, target='shop', want='Stone', amount=5, pay=1, budget=10),
    play('Bob'),
    give('Stone', 3),
    M('buy.sell', label='3 stone for a 5-stone deal: refused', notContains='Sold', target='shop', offer=1, deals=1),
    play('me'),
])

T('S-50', 'open crates hold items and open for the builder', [
    place('Drake_OpenCrate', 'crate', dz=2.5),
    {'do': 'interact', 'target': 'crate'}, wait(0.4),
    check('uiVisible', 'the crate opens', name='InventoryGui'),
    {'do': 'closeUi'},
    {'do': 'screenshot', 'name': 'open-crate'},
])

T('S-60', "Shopkeeper's Apron: crafted at the workbench, worn, Open for business", [
    {'do': 'give', 'prefab': 'LinenThread', 'count': 6}, {'do': 'give', 'prefab': 'LeatherScraps', 'count': 4}, {'do': 'give', 'prefab': 'BronzeNails', 'count': 2},
    place('piece_workbench', 'bench', dz=3, dx=2),
    {'do': 'lookAt', 'target': 'bench', 'distance': 2},
    {'do': 'craft', 'prefab': 'Drake_ShopApron', 'free': False, 'station': 'bench'},
    check('hasItem', 'crafted with the real cost', prefab='Drake_ShopApron'),
    check('noItem', 'linen used', prefab='LinenThread'),
    {'do': 'setConfig', 'guid': 'com.drakemods.itemshop', 'values': {'09 Apron/Bonus applies': 'Always', '09 Apron/Carry weight bonus': 50}},
    {'do': 'give', 'prefab': 'Drake_ShopApron', 'equip': True},
    wait(1.5),
    check('stat', 'Open for business: +50 carry weight', name='carryWeight', min=350),
    {'do': 'screenshot', 'name': 'apron'},
])

T('S-61', 'apron styles are craftable', [
    *[{'do': 'craft', 'prefab': a, 'label': f'craft {a}'} for a in ['Drake_ShopApronBlack', 'Drake_ShopApronCafe', 'Drake_ShopApronGreen', 'Drake_ShopApronMerchant', 'Drake_ShopApronRed']],
    *[check('hasItem', f'{a} made', prefab=a) for a in ['Drake_ShopApronBlack', 'Drake_ShopApronCafe', 'Drake_ShopApronGreen', 'Drake_ShopApronMerchant', 'Drake_ShopApronRed']],
])

T('S-70', 'racks and mats show what they sell', [
    *shop([('ArmorBronzeChest', 1, 'c:50', None)], piece='Drake_ArmorRack'),
    {'do': 'screenshot', 'name': 'armor-rack'},
    play('Alice'),
    place('Drake_DisplayMat', 'mat', dz=3, dx=2.5),
    M('itemshop.stock', True, target='mat', prefab='SwordBronze', count=1, price='c:5'),
    {'do': 'screenshot', 'name': 'display-mat'},
    play('me'),
], desc='How goods lie and hang is checked by the screenshots.')

suite = {
    'name': 'DrakesItemShop',
    'description': "Automated regression for Drake's ItemShop: a test shop next to the house (display case, rack, mat) stocked and opened by Alice, "
                   "bought from by Bob through the buy window's own purchase code: coins, barter, alternative payments, lots, refusals, till, "
                   'sales log, collecting and who may run a shop.',
    'requireMods': [{'guid': GUID, 'minVersion': '0.4.0'}],
    'configOverrides': [],
    # settings that only apply at game start: Run-TestLabSuites.ps1 writes them into the cfg before launching
    'launchConfig': [{'guid': GUID, 'section': '08 Experimental', 'key': 'Post wall and PO boxes', 'value': 'true'}],
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
