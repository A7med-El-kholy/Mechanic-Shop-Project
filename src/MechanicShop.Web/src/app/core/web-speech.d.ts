/**
 * Web Speech API type declarations.
 * Browsers expose `webkitSpeechRecognition` (Chrome/Edge/Safari) and
 * `SpeechSynthesis` for text-to-speech. These are not in the standard
 * TypeScript DOM lib, so we declare them here.
 */

interface SpeechRecognitionEvent extends Event {
    resultIndex: number;
    results: SpeechRecognitionResultList;
}

interface SpeechRecognitionResultList {
    readonly length: number;
    item(index: number): SpeechRecognitionResult;
    [index: number]: SpeechRecognitionResult;
}

interface SpeechRecognitionResult {
    readonly isFinal: boolean;
    readonly [index: number]: SpeechRecognitionAlternative;
}

interface SpeechRecognitionAlternative {
    readonly transcript: string;
    readonly confidence: number;
}

interface SpeechRecognitionErrorEvent extends Event {
    error: string;
    message: string;
}

interface SpeechRecognition extends EventTarget {
    continuous: boolean;
    interimResults: boolean;
    lang: string;
    start(): void;
    stop(): void;
    abort(): void;
    onstart: ((ev: Event) => any) | null;
    onend: ((ev: Event) => any) | null;
    onresult: ((ev: SpeechRecognitionEvent) => any) | null;
    onerror: ((ev: SpeechRecognitionErrorEvent) => any) | null;
}

interface SpeechSynthesisUtteranceEvent extends Event {
    charIndex: number;
    elapsedTime: number;
}

interface SpeechSynthesisUtterance {
    text: string;
    lang: string;
    voice: SpeechSynthesisVoice | null;
    rate: number;
    pitch: number;
    volume: number;
    onstart: ((ev: SpeechSynthesisUtteranceEvent) => any) | null;
    onend: ((ev: SpeechSynthesisUtteranceEvent) => any) | null;
    onerror: ((ev: SpeechSynthesisUtteranceEvent) => any) | null;
    new (text?: string): SpeechSynthesisUtterance;
}

interface SpeechSynthesisVoice {
    readonly voiceURI: string;
    readonly name: string;
    readonly lang: string;
    readonly localService: boolean;
    readonly default: boolean;
}

interface SpeechSynthesis {
    speak(utterance: SpeechSynthesisUtterance): void;
    cancel(): void;
    getVoices(): SpeechSynthesisVoice[];
    readonly pending: boolean;
    readonly speaking: boolean;
}

interface Window {
    webkitSpeechRecognition?: new () => SpeechRecognition;
    SpeechRecognition?: new () => SpeechRecognition;
    speechSynthesis?: SpeechSynthesis;
}
