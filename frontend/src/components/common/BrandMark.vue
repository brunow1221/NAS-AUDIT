<template>
  <div class="brand-mark" aria-hidden="true">
    <span class="beam beam-a" />
    <span class="beam beam-b" />
    <span class="beam beam-c" />
  </div>
</template>

<style scoped>
.brand-mark { position: relative; width: 38px; height: 38px; flex: 0 0 38px; border-radius: 11px; background: #e0edff; overflow: hidden; }
.beam { position: absolute; display: block; width: 9px; border-radius: 3px; background: #1d4ed8; transform: skew(-18deg); }
.beam-a { height: 23px; left: 8px; top: 8px; }
.beam-b { height: 16px; left: 19px; top: 15px; background: #3b82f6; }
.beam-c { width: 21px; height: 5px; left: 9px; top: 26px; background: #0f2f68; }
</style>

