"""Writes the DrakesTestLab suite for ItemShop's vending machines into Tests/suites/DrakesItemShop-Vending
(run: python gen_vending_suite.py). The build copies it into the test profiles; Tests/ is never packaged.
Hooks: Testing/VendingTestHooks.cs (itemshop.vending.*)."""
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'DrakesItemShop-Vending')
GUID = 'com.drakemods.itemshop'
NOERR = {'check': 'noErrors', 'source': 'DrakesItemShop'}
MACHINES = [('Drake_VendingHaldor', 'haldor'), ('Drake_VendingHildir', 'hildir'), ('Drake_VendingBogWitch', 'bogwitch')]
OUT_OF_ORDER = 'This seems to be out of order'


def H(name, expect=None, label=None, contains=None, notContains=None, regex=None, stop=False, **args):
    s = {'do': 'hook', 'name': 'itemshop.vending.' + name}
    if args:
        s['args'] = args
    for k, v in (('expect', expect), ('contains', contains), ('notContains', notContains), ('regex', regex), ('label', label)):
        if v is not None:
            s[k] = v
    if stop:
        s['stop'] = True
    return s


def wait(s=1.5):
    return {'do': 'wait', 'seconds': s}


def check(check_, label=None, **f):
    s = {'do': 'check', 'check': check_, **f}
    if label:
        s['label'] = label
    return s


def place(prefab, tag, dz=3.5, dx=0, label=None, **f):
    s = {'do': 'place', 'prefab': prefab, 'dz': dz, 'dx': dx, 'as': tag}
    if label:
        s['label'] = label
    s.update(f)
    return s


def state(tag, label, **f):
    return H('state', label=label, target=tag, **f)


def key(k, on=True):
    return H('setKey', True, f"world key {k} {'set' if on else 'removed'}", key=k, on=on)


def config(values):
    return {'do': 'setConfig', 'values': values}


def not_admin(on=True):
    return H('notAdmin', on, 'act as a player who is not an admin' if on else 'back to admin', on=on)


tests = {}


def T(tid, title, steps, desc=''):
    tests[tid] = {'name': f'{tid} {title}', 'description': desc, 'steps': steps, 'asserts': [NOERR]}


# ---- placing (admins only) ----
T('V-01', 'admin places all three', [
    *[place(prefab, tag, dx=dx, label=f'{prefab} builds from the hammer (the host is an admin)')
      for (prefab, tag), dx in zip(MACHINES, (-1.7, 0, 1.7))],
    wait(),
    state('hildir', 'Hildir needs no key: in service', contains='service=on'),
    state('haldor', 'Haldor needs Eikthyr: out of order', contains='service=off'),
    state('bogwitch', 'the Bog Witch needs The Elder: out of order', contains='service=off'),
], desc='Each machine is listed in the hammer, switched on and built the real way; placed fresh, the unlock keys decide which work.')

T('V-02', 'not placeable by players', [
    config({'01 General/AdminOnlyBuildPieces': 'false'}),
    not_admin(),
    *[place(prefab, tag, expect=False, label=f'a player who is not an admin cannot build {prefab}') for prefab, tag in MACHINES],
    check('message', 'the refusal says why', contains='Only admins can build vending machines', kind='Card'),
    place('Drake_DisplayCase', 'case', dz=3, label='control: the same player still builds an ordinary shop piece (AdminOnlyBuildPieces off)'),
    not_admin(False),
], desc='Vending machines are admin-only whatever AdminOnlyBuildPieces says; other shop pieces follow that setting.')

T('V-03', 'AdminOnlyBuildPieces on', [
    config({'01 General/AdminOnlyBuildPieces': 'true'}),
    not_admin(),
    place('Drake_DisplayCase', 'case', dz=3, expect=False, label='with AdminOnlyBuildPieces on, a non-admin cannot build shop pieces either'),
    place('Drake_VendingHaldor', 'haldor', expect=False, label='nor a vending machine'),
    not_admin(False),
    place('Drake_VendingHaldor', 'haldor', label='an admin builds the vending machine'),
])

# ---- world keys: in service / out of order ----
T('V-04', 'out of order until unlocked', [
    place('Drake_VendingHaldor', 'm'),
    wait(),
    check('globalKey', 'fresh world: Eikthyr not defeated', key='defeated_eikthyr', expect=False),
    state('m', 'out of order: dark, no light, empty shelves', regex='service=off;glow=dark;light=off;items=0;cards=0;.*'),
    H('hover', label='the hover says so', target='m', contains=OUT_OF_ORDER),
    H('hover', label='and offers no Browse wares', target='m', notContains='Browse'),
    H('use', False, 'E does not open the store', target='m'),
    check('message', 'E says it is out of order', contains=OUT_OF_ORDER, kind='Card'),
])

T('V-05', 'Haldor works once Eikthyr is down', [
    place('Drake_VendingHaldor', 'm'),
    wait(),
    key('defeated_eikthyr'),
    wait(),
    state('m', 'in service within a second: glowing, stocked', regex='service=on;glow=lit;.*items=[1-9].*'),
    state('m', 'nothing locked is shown on the shelves', contains='lockedShown=0'),
    H('hover', label='hover offers Browse wares', target='m', contains='Browse wares'),
    H('use', True, 'E opens the store', target='m'),
    H('closeStore', True),
])

T('V-06', 'Bog Witch on and off with The Elder', [
    place('Drake_VendingBogWitch', 'm'),
    wait(),
    state('m', 'locked at first', contains='service=off'),
    key('defeated_gdking'),
    wait(),
    state('m', 'The Elder down: in service', regex='service=on;glow=lit;.*items=[1-9].*'),
    H('use', True, 'E opens her store', target='m'),
    H('closeStore', True),
    key('defeated_gdking', on=False),
    wait(),
    state('m', 'key removed again: out of order, dark, shelves cleared', regex='service=off;glow=dark;light=off;items=0;cards=0;.*'),
    H('use', False, 'E no longer opens the store', target='m'),
], desc='Removing the key (a server rolling back progress) switches the machine off again.')

T('V-07', 'Hildir from the start', [
    place('Drake_VendingHildir', 'm'),
    wait(),
    state('m', 'no keys needed: in service', regex='service=on;glow=lit;.*items=[1-9].*'),
    H('use', True, 'E opens her store', target='m'),
    H('closeStore', True),
])

# ---- configs ----
T('V-08', 'unlock keys are configurable', [
    place('Drake_VendingHaldor', 'm'),
    config({'10 Vending/Haldor unlock keys': ''}),
    wait(),
    state('m', 'empty key list: works with no boss down', contains='service=on'),
    config({'10 Vending/Haldor unlock keys': 'defeated_bonemass'}),
    key('defeated_eikthyr'),
    wait(),
    state('m', 'now it wants Bonemass: Eikthyr alone is not enough', contains='service=off'),
    key('defeated_bonemass'),
    wait(),
    state('m', 'Bonemass down: in service', contains='service=on'),
    config({'10 Vending/Haldor unlock keys': 'defeated_bonemass, defeated_dragon'}),
    wait(),
    state('m', 'a list needs every key: Moder missing', contains='service=off'),
    key('defeated_dragon'),
    wait(),
    state('m', 'all keys set: in service', contains='service=on'),
])

T('V-09', 'out of order message is configurable', [
    config({'10 Vending/Out of order message': 'Closed for repairs'}),
    place('Drake_VendingHaldor', 'm'),
    wait(),
    H('hover', label='hover shows the server text', target='m', contains='Closed for repairs'),
    H('use', False, target='m'),
    check('message', 'and so does E', contains='Closed for repairs', kind='Card'),
])

T('V-10', 'bay light setting', [
    place('Drake_VendingHildir', 'm'),
    wait(),
    state('m', 'Night (default) at noon: off, no daytime glare', contains='light=off'),
    config({'10 Vending/Bay light': 'Always'}),
    wait(),
    state('m', 'Always: on', contains='light=on'),
    config({'10 Vending/Bay light': 'Off'}),
    wait(),
    state('m', 'Off: off', contains='light=off'),
])

T('V-11', 'sold out cards hide locked goods', [
    place('Drake_VendingHaldor', 'm'),
    key('defeated_eikthyr'),
    wait(),
    state('m', 'items not unlocked yet stand behind Sold out cards', regex='.*cards=[1-9].*'),
    state('m', 'and none of them is shown', contains='lockedShown=0'),
], desc='Haldor sells more once later bosses fall; until then those slots carry the shop card, not the item.')

# ---- buying ----
T('V-12', 'buy from a machine', [
    {'do': 'give', 'prefab': 'Coins', 'count': 999},
    place('Drake_VendingHaldor', 'm'),
    key('defeated_eikthyr'),
    wait(),
    H('buy', 'ok', 'the first item for sale, bought through the store window: coins down by its price, its stack in the bag (never dropped)', target='m'),
    check('hasItem', 'the coins were taken (they leave the world)', prefab='Coins', max=998),
    state('m', 'the vend plays', contains='vending=yes'),
    wait(5),
    state('m', 'and is over: the shelf is restocked', regex='.*items=[1-9].*vending=no'),
    H('closeStore', True),
    {'do': 'screenshot', 'name': 'vending-after-buy'},
], desc='Coins removed, the item straight into the inventory, the shelf animation runs and the shelf refills.')

# ---- as placed: survives a relog, real E ----
T('V-13', 'placed machine survives a relog', [
    place('Drake_VendingHildir', 'm', keep=True),
    wait(),
    {'do': 'relog'},
    wait(2),
    H('state', label='after the relog the machine (the nearest one) is still in service and stocked', regex='service=on;glow=lit;.*items=[1-9].*'),
    H('use', True, 'and opens the store'),
    H('closeStore', True),
])

T('V-14', 'E on the machine', [
    place('Drake_VendingHildir', 'm'),
    wait(),
    {'do': 'interact', 'target': 'm'},
    H('storeOpen', True, 'pressing E on it opens the store'),
    H('closeStore', True),
    place('Drake_VendingHaldor', 'h', dx=2),
    wait(),
    {'do': 'interact', 'target': 'h'},
    H('storeOpen', False, 'pressing E on a locked one does not'),
    check('message', 'it says it is out of order', contains=OUT_OF_ORDER, kind='Card'),
], desc='The real E key path: through the piece, not a hook.')

suite = {
    'name': 'DrakesItemShop-Vending',
    'description': "Automated regression for ItemShop's vending machines (server trader replacements): placing (admins only, "
                   'AdminOnlyBuildPieces), world-key gating (in service / out of order, on and off again), the vending settings, '
                   'buying through the real store window, sold out cards, persistence.',
    'requireMods': [{'guid': GUID, 'minVersion': '0.6.0'}],
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
print(len(tests), 'vending tests')
