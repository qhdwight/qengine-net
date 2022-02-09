using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Game.Graphic;

public record struct Graphics
{
    public IWindow? window;
    public Vk? vk;

    public Instance instance;

    public ExtDebugUtils? debugUtils;
    public DebugUtilsMessengerEXT debugMessenger;
    public KhrSurface? khrSurface;
    public SurfaceKHR surface;

    public PhysicalDevice physicalDevice;
    public Device device;

    public Queue graphicsQueue;
    public Queue presentQueue;

    public KhrSwapchain? khrSwapChain;
    public SwapchainKHR swapChain;
    public Image[]? swapChainImages;
    public Format swapChainImageFormat;
    public Extent2D swapChainExtent;
    public ImageView[]? swapChainImageViews;
    public Framebuffer[]? swapChainFramebuffers;

    public RenderPass renderPass;
    public PipelineLayout pipelineLayout;
    public Pipeline graphicsPipeline;

    public CommandPool commandPool;
    public CommandBuffer[]? commandBuffers;

    public Semaphore[]? imageAvailableSemaphores;
    public Semaphore[]? renderFinishedSemaphores;
    public Fence[]? inFlightFences;
    public Fence[]? imagesInFlight;
    public int currentFrame = 0;
}