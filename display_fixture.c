#include <stdint.h>
#include <stddef.h>
#include <stdlib.h>
#include <string.h>
// Independent native fixture modelling the verified EntranceRoom/ARoom layout:
// the derived _dialogManager is null; the inherited DialogManager is live.
static int room_class,base_class,dialog_class,view_class,day_class,input_class,guest_class;
struct obj { void *klass; };
static struct obj room={&room_class},dialog={&dialog_class},view={&view_class},day={&day_class},guest={&guest_class},other={&guest_class};
static int current_day,current_type,knocking,has_dialog,has_view,active,shown,has_day,legacy_reads;
static int field_ids[64];
static int input_method,day_method,assembly,image,key_type;
static void *assemblies[]={&assembly};
static int boxed;
static void *handles[256];
static unsigned next_handle;
void fixture_setup(int d,int t,int k,int hd,int hv,int a,int sh,int dy) {
 current_day=d;current_type=t;knocking=k;has_dialog=hd;has_view=hv;active=a;shown=sh;has_day=dy;
}
void *fixture_room(void) { return &room; }
void *fixture_guest(void) { return &guest; }
int fixture_legacy_reads(void) { return legacy_reads; }
void *il2cpp_gchandle_new(void *o,uint8_t pinned) { (void)pinned; unsigned i=(next_handle++)%256; handles[i]=o;return &handles[i]; }
void *il2cpp_gchandle_get_target(void *h) { return *(void **)h; }
void il2cpp_gchandle_free(void *h) { *(void **)h=NULL; }
void *il2cpp_object_get_class(struct obj *o) { return o->klass; }
void *il2cpp_class_get_parent(void *k) { return k==&room_class?&base_class:NULL; }
void *il2cpp_class_get_field_from_name(void *k,const char *name) {
 int n=0;
 if(k==&room_class) {
  if(!strcmp(name,"_currentCharacter")) n=1;
  else if(!strcmp(name,"_isSomeoneKnockingNow")) n=2;
  else if(!strcmp(name,"_dialogManager")) n=3;
 } else if(k==&base_class) {
  if(!strcmp(name,"DialogManager")) n=11;
  else if(!strcmp(name,"DayNightController")) n=12;
 } else if(k==&dialog_class && !strcmp(name,"_dialogView")) n=21;
 else if(k==&view_class) {
  if(!strcmp(name,"_isActive")) n=31;
  else if(!strcmp(name,"_currentCharacter")) n=32;
 }
 return n?&field_ids[n]:NULL;
}
void il2cpp_field_get_value(void *o,void *f,void *out) {
 (void)o; ptrdiff_t id=(int *)f-field_ids;
 switch(id) {
  case 1: *(int *)out=current_type;break;
  case 2: *(uint8_t *)out=knocking;break;
  case 3: legacy_reads++;*(void **)out=NULL;break;
  case 11: *(void **)out=has_dialog?&dialog:NULL;break;
  case 12: *(void **)out=has_day?&day:NULL;break;
  case 21: *(void **)out=has_view?&view:NULL;break;
  case 31: *(uint8_t *)out=active;break;
  case 32: *(void **)out=shown==1?&guest:shown==2?&other:NULL;break;
  default: abort();
 }
}
void *il2cpp_domain_get(void) { return &assembly; }
void **il2cpp_domain_get_assemblies(void *domain,size_t *count) { (void)domain;*count=1;return assemblies; }
void *il2cpp_assembly_get_image(void *a) { (void)a;return &image; }
const char *il2cpp_image_get_name(void *i) { (void)i;return "UnityEngine.InputLegacyModule.dll"; }
void *il2cpp_class_from_name(void *i,const char *ns,const char *name) { (void)i;(void)ns;return !strcmp(name,"Input")?&input_class:NULL; }
void *il2cpp_class_get_methods(void *k,void **it) {
 if(*it) return NULL;
 *it=(void *)1;
 return k==&input_class?&input_method:k==&day_class?&day_method:NULL;
}
const char *il2cpp_method_get_name(void *m) { return m==&input_method?"GetKeyDown":"get_Day"; }
uint32_t il2cpp_method_get_param_count(void *m) { return m==&input_method?1:0; }
void *il2cpp_method_get_param(void *m,uint32_t i) { (void)m;(void)i;return &key_type; }
char *il2cpp_type_get_name(void *t) { (void)t;return strdup("UnityEngine.KeyCode"); }
void il2cpp_free(void *p) { free(p); }
void *il2cpp_runtime_invoke(void *method,void *o,void **args,void **ex) {
 (void)o;(void)args;*ex=NULL;boxed=method==&day_method?current_day:0;return &boxed;
}
void *il2cpp_object_unbox(void *o) { return o; }
