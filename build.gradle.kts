import org.gradle.api.tasks.Exec

plugins {
    base
}

val androidProject = layout.projectDirectory.file("src/MudClient.Android/MudClient.Android.csproj")
val androidArtifacts = layout.projectDirectory.dir(".artifacts/android-studio")

fun androidSdkDirectory(): String {
    val configured = providers.environmentVariable("ANDROID_SDK_ROOT").orNull
        ?: providers.environmentVariable("ANDROID_HOME").orNull
    if (!configured.isNullOrBlank()) {
        return configured
    }

    val localAppData = providers.environmentVariable("LOCALAPPDATA").orNull
        ?: error("Nie znaleziono LOCALAPPDATA ani ANDROID_SDK_ROOT.")
    return file("$localAppData/Android/Sdk").absolutePath
}

tasks.register<Exec>("buildAndroid") {
    group = "android"
    description = "Buduje debugowe APK projektu Avalonia/.NET."

    val sdkDirectory = androidSdkDirectory()
    val javaDirectory = System.getProperty("java.home")

    commandLine(
        "dotnet",
        "build",
        androidProject.asFile.absolutePath,
        "-c", "Debug",
        "-m:1",
        "--artifacts-path", androidArtifacts.asFile.absolutePath,
        "-p:EmbedAssembliesIntoApk=true",
        "-p:AndroidSdkDirectory=$sdkDirectory",
        "-p:JavaSdkDirectory=$javaDirectory",
    )
}

tasks.named("build") {
    dependsOn("buildAndroid")
}

tasks.register<Exec>("runAndroid") {
    group = "android"
    description = "Uruchamia AVD, wykonuje pelny reinstall i startuje aplikacje."

    val avdName = providers.gradleProperty("avd").orElse("Pixel_8")
    environment("KMC_NO_PAUSE", "1")
    commandLine(
        "cmd",
        "/c",
        layout.projectDirectory.file("run-android.bat").asFile.absolutePath,
        avdName.get(),
    )
}
