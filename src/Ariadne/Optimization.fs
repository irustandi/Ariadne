﻿module Ariadne.Optimization

open MathNet.Numerics.Statistics
open MathNet.Numerics.Statistics.Mcmc
open MathNet.Numerics.Distributions

open Ariadne
open Ariadne.Kernels
open Ariadne.GaussianProcess

/// Type alias for kernel parameters
type Parameters = float []

module MetropolisHastings =
    type Settings =
        { Burnin : int; Lag : int; SampleSize : int }

    let defaultSettings = 
        { Burnin = 50; Lag = 5; SampleSize = 50}


/// Sample Gaussian process hyperparameters using MCMC 
/// with Metropolis-Hastings sampler
let sampleMetropolisHastings 
        (logLikelihood : Parameters -> float)
        (transitionKernel: Parameters -> Parameters -> float )
        (proposalSampler: Parameters -> Parameters) 
        (settings: MetropolisHastings.Settings)
        (initialLocation : Parameters) =

    let nParams = initialLocation |> Array.length

    // Call Metropolis-Hastings sampler from Math.NET Numerics    
    let ms = new MetropolisHastingsSampler<Parameters>(initialLocation,
                    (fun x -> logLikelihood x), 
                    (fun x y -> transitionKernel x y),
                    (fun x -> proposalSampler x), settings.Burnin)
    
    // sample burnin iterations
    let sample1 = [| ms.Sample() |]
    // sample the rest of the iterations with thinning
    ms.BurnInterval <- settings.Lag
    let samples = ms.Sample(settings.SampleSize)

    // Summarize samples - return their mean value 
    [| for p in 0..nParams-1 -> 
            // compute average in log space
            samples |> Array.averageBy (fun s ->  log s.[p])
            |> exp |]

module SquaredExponential =

    /// Extension of Metropolis-Hastings sampler to find new values of hyperparameters
    /// for squared exponential kernel.
    let optimizeMetropolisHastings data settings (prior : SquaredExponential.Prior) (initialKernel:SquaredExponential.SquaredExponential) = 
        let initialLocation = initialKernel.Parameters               
        let proposalDist = Array.init 3 (fun x -> Normal.WithMeanVariance(0.0, 0.01))

        let proposalSampler parameters = 
            SquaredExponential.randomWalkProposal proposalDist parameters

        let transitionKernel oldParams newParams = 
            SquaredExponential.transitionProbability proposalDist oldParams newParams

        let logLikFunction parameters = 
            let se = SquaredExponential.ofParameters parameters
            let gp = GaussianProcess.GaussianProcess(se.Kernel, Some(se.NoiseVariance))
            (gp.LogLikelihood [data]) + prior.DensityLn(se)

        let newParams = 
            initialLocation
            |> sampleMetropolisHastings logLikFunction transitionKernel proposalSampler settings
            |> Array.ofSeq
 
        newParams|> SquaredExponential.ofParameters



module GradientDescent = 
    type Settings = 
        {Iterations : int; StepSize : float}

    let defaultSettings = 
        {Iterations = 100; StepSize = 0.05}

/// Optimize Gaussian process hyperparameters using simple gradient
/// descent 
let gradientDescent 
        (gradientFun : Parameters -> Parameters)
        (settings : GradientDescent.Settings)
        (initialLocation : Parameters) =

    let update xs = 
        let gs = gradientFun xs
        Array.map2 (fun x g -> x + settings.StepSize*g) xs gs

    [1..settings.Iterations]
    |> List.fold (fun parameters iter -> update parameters) initialLocation

/// Use only a subsample of data to do gradient descent
let stochasticGradientDescent
        (initialLocation : Parameters) 
        (gradientFun : Observation<'T> seq -> Parameters -> Parameters) 
        batchSize nIterations =
    
    // split data into batch-sized groups or just sample groups of the appropriate batch size?
    0.0

        



