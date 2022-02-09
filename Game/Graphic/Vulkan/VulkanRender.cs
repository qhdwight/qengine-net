using System;
using Silk.NET.Vulkan;

namespace Game.Graphic.Vulkan;

internal static unsafe partial class VulkanGraphics
{
    public static void Render(ref Graphics graphics)
    {
        graphics.vk!.WaitForFences(graphics.device, 1, graphics.inFlightFences![graphics.currentFrame], true, ulong.MaxValue);

        uint imageIndex = 0;
        Result result = graphics.khrSwapChain!.AcquireNextImage(graphics.device, graphics.swapChain, ulong.MaxValue,
                                                                graphics.imageAvailableSemaphores![graphics.currentFrame], default,
                                                                ref imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapChain(ref graphics);
            return;
        }
        if (result != Result.Success && result != Result.SuboptimalKhr)
            throw new Exception("Failed to acquire swap chain image!");

        UpdateUniformBuffer(ref graphics, imageIndex);

        if (graphics.imagesInFlight![imageIndex].Handle != default)
            graphics.vk!.WaitForFences(graphics.device, 1, graphics.imagesInFlight[imageIndex], true, ulong.MaxValue);
        graphics.imagesInFlight[imageIndex] = graphics.inFlightFences![graphics.currentFrame];

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo
        };

        Semaphore* waitSemaphores = stackalloc[] { graphics.imageAvailableSemaphores![graphics.currentFrame] };
        PipelineStageFlags* waitStages = stackalloc[] { PipelineStageFlags.PipelineStageColorAttachmentOutputBit };

        CommandBuffer buffer = graphics.commandBuffers![imageIndex];

        submitInfo = submitInfo with
        {
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,

            CommandBufferCount = 1,
            PCommandBuffers = &buffer
        };

        Semaphore* signalSemaphores = stackalloc[] { graphics.renderFinishedSemaphores![graphics.currentFrame] };
        submitInfo = submitInfo with
        {
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores
        };

        graphics.vk!.ResetFences(graphics.device, 1, graphics.inFlightFences[graphics.currentFrame]);

        if (graphics.vk!.QueueSubmit(graphics.graphicsQueue, 1, submitInfo, graphics.inFlightFences[graphics.currentFrame]) != Result.Success)
            throw new Exception("Failed to submit draw command buffer!");

        SwapchainKHR* swapChains = stackalloc[] { graphics.swapChain };
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,

            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,

            SwapchainCount = 1,
            PSwapchains = swapChains,

            PImageIndices = &imageIndex
        };

        graphics.khrSwapChain!.QueuePresent(graphics.presentQueue, presentInfo);

        graphics.currentFrame = (graphics.currentFrame + 1) % MaxFramesInFlight;

        graphics.vk!.DeviceWaitIdle(graphics.device);
    }
}