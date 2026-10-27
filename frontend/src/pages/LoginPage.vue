<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Eye, EyeOff, LockKeyhole, ServerCog, ShieldCheck } from '@lucide/vue'
import BrandMark from '../components/common/BrandMark.vue'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const email = ref('')
const password = ref('')
const visible = ref(false)
const error = ref('')

async function submit() {
  error.value = ''
  try {
    await auth.login(email.value, password.value)
    await router.replace(typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'No fue posible iniciar sesión.'
  }
}
</script>

<template>
  <main class="login-page">
    <section class="intro">
      <div class="intro__content">
        <div class="brand-row"><BrandMark /><div><strong>BCMETALS</strong><span>NAS AUDIT</span></div></div>
        <p class="eyebrow">Control documental interno</p>
        <h1>Visibilidad clara sobre la actividad del NAS.</h1>
        <p class="lead">Consulte evidencia histórica de accesos y operaciones SMB sin intervenir los documentos empresariales.</p>
        <div class="features">
          <div><ShieldCheck :size="20" /><span><strong>Acceso restringido</strong><small>Sesiones protegidas para el equipo TI</small></span></div>
          <div><ServerCog :size="20" /><span><strong>Procesamiento local</strong><small>Diseñado para operar dentro del NAS</small></span></div>
          <div><LockKeyhole :size="20" /><span><strong>Solo auditoría</strong><small>Sin acciones sobre archivos compartidos</small></span></div>
        </div>
      </div>
    </section>
    <section class="form-side">
      <form class="login-card panel" @submit.prevent="submit">
        <p class="eyebrow">Portal administrativo</p>
        <h2>Bienvenido</h2>
        <p class="form-copy">Ingrese con su cuenta autorizada para consultar la auditoría.</p>
        <div><label class="label" for="email">Correo</label><input id="email" v-model.trim="email" class="field" type="email" autocomplete="username" required /></div>
        <div><label class="label" for="password">Contraseña</label><div class="password"><input id="password" v-model="password" class="field" :type="visible ? 'text' : 'password'" autocomplete="current-password" required /><button type="button" :aria-label="visible ? 'Ocultar contraseña' : 'Mostrar contraseña'" @click="visible = !visible"><EyeOff v-if="visible" :size="18" /><Eye v-else :size="18" /></button></div></div>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <button class="btn-primary submit" type="submit" :disabled="auth.loading"><LockKeyhole :size="17" />{{ auth.loading ? 'Verificando…' : 'Ingresar de forma segura' }}</button>
        <p class="footnote">El acceso queda reservado al personal autorizado de BCMETALS.</p>
      </form>
    </section>
  </main>
</template>

<style scoped>
.login-page { display: grid; min-height: 100vh; grid-template-columns: minmax(360px, 1.05fr) minmax(420px, .95fr); background: #edf5fb; }
.intro { position: relative; display: grid; align-items: center; overflow: hidden; padding: 64px clamp(40px, 7vw, 105px); color: #fff; background: #0d2f65; }
.intro::after { content: ''; position: absolute; right: -150px; bottom: -210px; width: 500px; height: 500px; border: 80px solid rgba(96,165,250,.1); border-radius: 50%; }
.intro__content { position: relative; z-index: 1; max-width: 610px; }
.brand-row { display: flex; align-items: center; gap: 12px; margin-bottom: 72px; }.brand-row strong,.brand-row span { display: block; }.brand-row strong { letter-spacing: .06em; }.brand-row span { color: #93c5fd; font-size: 11px; font-weight: 800; letter-spacing: .2em; }
.eyebrow { margin: 0 0 11px; color: #60a5fa; font-size: 11px; font-weight: 800; letter-spacing: .16em; text-transform: uppercase; }
h1 { max-width: 560px; margin: 0; font-size: clamp(36px, 4.4vw, 60px); line-height: 1.03; letter-spacing: -.045em; }.lead { max-width: 530px; margin: 24px 0 40px; color: #c8ddf5; font-size: 17px; line-height: 1.65; }
.features { display: grid; gap: 18px; }.features > div { display: flex; align-items: center; gap: 13px; color: #93c5fd; }.features span,.features strong,.features small { display: block; }.features strong { color: #fff; font-size: 14px; }.features small { margin-top: 3px; color: #a9c1dc; font-size: 12px; }
.form-side { display: grid; place-items: center; padding: 34px; }.login-card { width: min(440px, 100%); padding: 36px; }.login-card h2 { margin: 0; font-size: 28px; }.form-copy { margin: 8px 0 27px; color: #64748b; line-height: 1.55; }.login-card > div { margin-bottom: 18px; }.password { position: relative; }.password input { padding-right: 46px; }.password button { position: absolute; right: 8px; top: 6px; display: grid; place-items: center; border: 0; padding: 6px; color: #64748b; background: transparent; }.submit { width: 100%; margin-top: 5px; }.form-error { border-radius: 9px; padding: 9px 11px; color: #b91c1c; background: #fef2f2; font-size: 13px; }.footnote { margin: 18px 0 0; color: #94a3b8; font-size: 11px; text-align: center; }
@media (max-width: 850px) { .login-page { grid-template-columns: 1fr; }.intro { display: none; }.form-side { padding: 20px; } }
</style>

