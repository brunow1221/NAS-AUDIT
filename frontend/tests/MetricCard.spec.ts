import { mount } from '@vue/test-utils'
import { FilePlus2 } from '@lucide/vue'
import MetricCard from '../src/components/dashboard/MetricCard.vue'

describe('MetricCard', () => {
  it('muestra la métrica y emite selección cuando es navegable', async () => {
    const wrapper = mount(MetricCard, {
      props: { label: 'Archivos creados', value: 12, icon: FilePlus2, clickable: true },
    })
    expect(wrapper.text()).toContain('Archivos creados')
    expect(wrapper.text()).toContain('12')
    await wrapper.trigger('click')
    expect(wrapper.emitted('select')).toHaveLength(1)
  })
})

