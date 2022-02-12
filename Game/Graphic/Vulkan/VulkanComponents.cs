using Silk.NET.Input;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Game.Graphic.Vulkan;

internal record struct VkMesh
{
    internal Buffer vertexBuffer;
    internal uint vertexBufferSize;
    internal DeviceMemory vertexBufferMemory;
    internal Buffer indexBuffer;
    internal uint indexBufferSize;
    internal DeviceMemory indexBufferMemory;
}

internal record struct VkGraphics
{
    internal IWindow? window;
    internal IInputContext? input;
    internal Vk? vk;

    internal Instance instance;

    internal ExtDebugUtils? debugUtils;
    internal DebugUtilsMessengerEXT debugMessenger;
    internal KhrSurface? khrSurface;
    internal SurfaceKHR surface;

    internal PhysicalDevice physicalDevice;
    internal Device device;

    internal Queue graphicsQueue;
    internal Queue presentQueue;

    internal KhrSwapchain? khrSwapChain;
    internal SwapchainKHR swapChain;
    internal Image[]? swapChainImages;
    internal Format swapChainImageFormat;
    internal Extent2D swapChainExtent;
    internal ImageView[]? swapChainImageViews;
    internal Framebuffer[]? swapChainFramebuffers;

    internal Image depthImage;
    internal DeviceMemory depthImageMemory;
    internal ImageView depthImageView;

    internal RenderPass renderPass;
    internal DescriptorSetLayout descriptorSetLayout;
    internal PipelineLayout pipelineLayout;
    internal Pipeline graphicsPipeline;

    internal Pipeline compPipeline;
    internal PipelineLayout compPipelineLayout;
    internal DescriptorSetLayout compDescSetLayout;

    internal CommandPool commandPool;

    internal Buffer[]? uniformBuffers;
    internal DeviceMemory[]? uniformBuffersMemory;

    internal DescriptorPool descriptorPool;
    internal DescriptorSet[]? descriptorSets;

    internal CommandBuffer[]? commandBuffers;

    internal Semaphore[]? imageAvailableSemaphores;
    internal Semaphore[]? renderFinishedSemaphores;
    internal Fence[]? inFlightFences;
    internal Fence[]? imagesInFlight;
    internal int currentFrame = 0;
}