import sys
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

path, begin, end = sys.argv[1], int(sys.argv[2], 16), int(sys.argv[3], 16)
show = int(sys.argv[4], 16) if len(sys.argv) > 4 else begin
pe = pefile.PE(path)
data = pe.get_memory_mapped_image()[begin:end]
engine = Cs(CS_ARCH_X86, CS_MODE_64)
for ins in engine.disasm(data, begin):
    if ins.address >= show:
        print(f'{ins.address:08x}  {ins.mnemonic:7} {ins.op_str}')
