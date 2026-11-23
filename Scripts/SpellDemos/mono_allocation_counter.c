/* 独立 Linux 游戏的可选托管分配计数，仅订阅 Mono 已报告的对象。 */
#define _GNU_SOURCE
#include <dlfcn.h>
#include <stdatomic.h>
#include <stdint.h>

typedef void (*AllocationCallback)(void *, void *);
static unsigned int (*object_size)(void *);
static void (*set_callback)(void *, AllocationCallback);
static void *profiler;
static _Atomic uint64_t bytes;

static void count_allocation(void *unused, void *object)
{
    (void)unused;
    atomic_fetch_add_explicit(&bytes, object_size(object), memory_order_relaxed);
}

int cultiway_allocations_begin(const char *runtime)
{
    if (!profiler) {
        void *library = dlopen(runtime, RTLD_NOW | RTLD_NOLOAD);
        if (!library)
            return 0;
        int (*enable)(void) = dlsym(library, "mono_profiler_enable_allocations");
        void *(*create)(void *) = dlsym(library, "mono_profiler_create");
        object_size = dlsym(library, "mono_object_get_size");
        set_callback = dlsym(library, "mono_profiler_set_gc_allocation_callback");
        if (enable && create && object_size && set_callback && enable())
            profiler = create(0);
        dlclose(library);
        if (!profiler)
            return 0;
    }

    atomic_store_explicit(&bytes, 0, memory_order_relaxed);
    set_callback(profiler, count_allocation);
    return 1;
}

uint64_t cultiway_allocations_total(void)
{
    return atomic_load_explicit(&bytes, memory_order_relaxed);
}

void cultiway_allocations_end(void)
{
    if (profiler)
        set_callback(profiler, 0);
}
