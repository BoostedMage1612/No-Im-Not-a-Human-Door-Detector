#include <stdint.h>
#include <stddef.h>
// Native test double with Unity 6's pointer-sized GC-handle ABI. Deliberately
// returns an opaque handle above 4 GiB, independently of the managed test code.
typedef void *Il2CppGCHandle;
static const uintptr_t token = UINT64_C(0x0000123456789028);
static void *target;
static int creates, reads, frees, bad;
void fixture_reset(void) { target=NULL; creates=reads=frees=bad=0; }
int fixture_creates(void) { return creates; }
int fixture_reads(void) { return reads; }
int fixture_frees(void) { return frees; }
int fixture_bad_handles(void) { return bad; }
Il2CppGCHandle il2cpp_gchandle_new(void *obj, uint8_t pinned) {
    (void)pinned; creates++; target=obj; return (Il2CppGCHandle)token;
}
void *il2cpp_gchandle_get_target(Il2CppGCHandle handle) {
    reads++;
    if ((uintptr_t)handle != token) { bad++; return NULL; }
    return target;
}
void il2cpp_gchandle_free(Il2CppGCHandle handle) {
    if ((uintptr_t)handle != token) { bad++; return; }
    frees++; target=NULL;
}
