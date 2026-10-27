import { mount } from '@vue/test-utils'
import DataModeSwitch from '../src/components/common/DataModeSwitch.vue'

describe('DataModeSwitch', () => {
  it('mantiene separados histórico y demo', async () => {
    const wrapper = mount(DataModeSwitch, { props: { modelValue: 'Historical' } })
    const buttons = wrapper.findAll('button')
    await buttons[1]!.trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['Demo'])
  })
})

