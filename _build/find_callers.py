import sys
import pefile

pe = pefile.PE(sys.argv[1])
target = int(sys.argv[2], 16)
data = pe.get_memory_mapped_image()
for addr in range(0x1000, 0x1EA9000 - 5):
    if data[addr] != 0xE8:
        continue
    displacement = int.from_bytes(data[addr + 1:addr + 5], 'little', signed=True)
    if addr + 5 + displacement == target:
        print(hex(addr))
